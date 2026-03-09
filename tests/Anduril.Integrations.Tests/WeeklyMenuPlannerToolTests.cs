using Anduril.Core.MenuPlanning;
using Anduril.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Integrations.Tests;

public class WeeklyMenuPlannerToolTests
{
    [Test]
    public async Task SavePreferencesAsync_OmittedOptionalArguments_PreserveExistingSettings()
    {
        var store = new FakeWeeklyMenuSubscriptionStore();
        await store.UpsertAsync(new WeeklyMenuSubscription
        {
            UserId = "user-1",
            RecipientEmail = "family@example.com",
            PreferenceSummary = "Original summary.",
            PeopleCount = 4,
            MealComplexity = "low effort",
            IncludeShoppingList = false,
            DeliverySchedule = "0 12 * * 3",
            IsRecurringEnabled = true,
            UpdatedAtUtc = new DateTime(2026, 03, 01, 10, 00, 00, DateTimeKind.Utc)
        });

        var tool = new WeeklyMenuPlannerTool(store, NullLogger<WeeklyMenuPlannerTool>.Instance);
        var result = await InvokeFunctionAsync(
            tool,
            "menu_planner_save_preferences",
            new Dictionary<string, object?>
            {
                ["userId"] = "user-1",
                ["preferenceSummary"] = "Updated summary."
            });

        var saved = await store.GetAsync("user-1");

        await Assert.That(result).Contains("Weekly menu preferences saved");
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.PreferenceSummary).IsEqualTo("Updated summary.");
        await Assert.That(saved.RecipientEmail).IsEqualTo("family@example.com");
        await Assert.That(saved.PeopleCount).IsEqualTo(4);
        await Assert.That(saved.MealComplexity).IsEqualTo("low effort");
        await Assert.That(saved.IncludeShoppingList).IsFalse();
        await Assert.That(saved.DeliverySchedule).IsEqualTo("0 12 * * 3");
        await Assert.That(saved.IsRecurringEnabled).IsTrue();
    }

    private static async Task<string> InvokeFunctionAsync(
        WeeklyMenuPlannerTool tool,
        string functionName,
        IDictionary<string, object?> arguments)
    {
        var function = tool.GetFunctions().First(function => function.Name.Equals(functionName, StringComparison.Ordinal));
        var result = await function.InvokeAsync(new AIFunctionArguments(arguments), CancellationToken.None);
        return result?.ToString() ?? string.Empty;
    }
}