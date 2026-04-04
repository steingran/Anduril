using Anduril.Core.Integrations;
using Anduril.Core.MenuPlanning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.Integrations;

/// <summary>
/// Exposes saved weekly menu preferences and recurring delivery settings as AI-callable functions.
/// </summary>
public sealed class WeeklyMenuPlannerTool(
    IWeeklyMenuSubscriptionStore subscriptionStore,
    ILogger<WeeklyMenuPlannerTool> logger) : IIntegrationTool
{
    public string Name => "weekly-menu-planner";

    public string Description =>
        "Stores weekly meal-planning preferences and recurring email delivery settings for users.";

    public bool IsAvailable => true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<AIFunction> GetFunctions() =>
    [
        AIFunctionFactory.Create(
            GetSavedPreferencesAsync,
            "menu_planner_get_saved_preferences",
            "Get the current user's saved weekly menu preferences and recurring email settings."),
        AIFunctionFactory.Create(
            SavePreferencesAsync,
            "menu_planner_save_preferences",
            "Save or update the current user's weekly menu preferences and recurring email settings."),
        AIFunctionFactory.Create(
            DisableWeeklyEmailAsync,
            "menu_planner_disable_weekly_email",
            "Disable recurring weekly menu emails for the current user while keeping saved preferences.")
    ];

    private async Task<string> GetSavedPreferencesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "A userId is required to look up saved weekly menu preferences.";

        var subscription = await subscriptionStore.GetAsync(userId.Trim());
        if (subscription is null)
            return $"No saved weekly menu preferences found for user '{userId.Trim()}'.";

        var recipientEmail = string.IsNullOrWhiteSpace(subscription.RecipientEmail)
            ? "not set"
            : subscription.RecipientEmail;

        return $"""
            Saved weekly menu preferences:
            - User ID: {subscription.UserId}
            - Recipient email: {recipientEmail}
            - People count: {subscription.PeopleCount}
            - Meal complexity: {subscription.MealComplexity}
            - Include shopping list: {(subscription.IncludeShoppingList ? "yes" : "no")}
            - Weekly email enabled: {(subscription.IsRecurringEnabled ? "yes" : "no")}
            - Delivery schedule: {subscription.DeliverySchedule}
            - Saved preferences summary: {subscription.PreferenceSummary}
            """;
    }

    private async Task<string> SavePreferencesAsync(
        string userId,
        string preferenceSummary,
        string recipientEmail = "",
        int peopleCount = -1,
        string mealComplexity = "",
        string includeShoppingList = "",
        string deliverySchedule = "",
        string enableWeeklyEmail = "")
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "A userId is required to save weekly menu preferences.";

        if (string.IsNullOrWhiteSpace(preferenceSummary))
            return "A preference summary is required to save weekly menu preferences.";

        var normalizedUserId = userId.Trim();
        var existing = await subscriptionStore.GetAsync(normalizedUserId);
        var normalizedEmail = string.IsNullOrWhiteSpace(recipientEmail)
            ? existing?.RecipientEmail
            : recipientEmail.Trim();
        bool? parsedEnableWeeklyEmail = string.IsNullOrWhiteSpace(enableWeeklyEmail)
            ? null
            : TryParseBool(enableWeeklyEmail);
        bool? parsedIncludeShoppingList = string.IsNullOrWhiteSpace(includeShoppingList)
            ? null
            : TryParseBool(includeShoppingList);
        int? parsedPeopleCount = peopleCount >= 0 ? peopleCount : null;
        var recurringEnabled = parsedEnableWeeklyEmail ?? existing?.IsRecurringEnabled ?? false;

        if (recurringEnabled && string.IsNullOrWhiteSpace(normalizedEmail))
            return "A recipient email address is required before recurring weekly menu delivery can be enabled.";

        var subscription = new WeeklyMenuSubscription
        {
            UserId = normalizedUserId,
            RecipientEmail = normalizedEmail,
            PreferenceSummary = preferenceSummary.Trim(),
            PeopleCount = Math.Max(1, parsedPeopleCount ?? existing?.PeopleCount ?? 2),
            MealComplexity = string.IsNullOrWhiteSpace(mealComplexity)
                ? existing?.MealComplexity ?? "balanced"
                : mealComplexity.Trim(),
            IncludeShoppingList = parsedIncludeShoppingList ?? existing?.IncludeShoppingList ?? true,
            DeliverySchedule = string.IsNullOrWhiteSpace(deliverySchedule)
                ? existing?.DeliverySchedule ?? "0 18 * * 0"
                : deliverySchedule.Trim(),
            IsRecurringEnabled = recurringEnabled,
            LastDeliveredAtUtc = existing?.LastDeliveredAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await subscriptionStore.UpsertAsync(subscription);

        logger.LogInformation(
            "Saved weekly menu preferences for user '{UserId}' with recurring email {Enabled}",
            subscription.UserId,
            subscription.IsRecurringEnabled);

        var emailText = string.IsNullOrWhiteSpace(subscription.RecipientEmail)
            ? "not set"
            : subscription.RecipientEmail;

        return $"""
            Weekly menu preferences saved for user '{subscription.UserId}'.
            - Recipient email: {emailText}
            - People count: {subscription.PeopleCount}
            - Meal complexity: {subscription.MealComplexity}
            - Include shopping list: {(subscription.IncludeShoppingList ? "yes" : "no")}
            - Weekly email enabled: {(subscription.IsRecurringEnabled ? "yes" : "no")}
            - Delivery schedule: {subscription.DeliverySchedule}
            """;
    }

    private async Task<string> DisableWeeklyEmailAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "A userId is required to disable weekly menu emails.";

        var normalizedUserId = userId.Trim();
        var existing = await subscriptionStore.GetAsync(normalizedUserId);
        if (existing is null)
            return $"No saved weekly menu preferences found for user '{normalizedUserId}'.";

        await subscriptionStore.DisableRecurringAsync(normalizedUserId);
        logger.LogInformation("Disabled weekly menu emails for user '{UserId}'", normalizedUserId);

        return $"Recurring weekly menu emails have been disabled for user '{normalizedUserId}'. Saved preferences were kept.";
    }

    private static bool? TryParseBool(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "1" or "on" => true,
            "false" or "no" or "0" or "off" => false,
            _ => null
        };
}
