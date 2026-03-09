using Anduril.Core.Communication;
using Anduril.Core.MenuPlanning;
using Anduril.Core.Skills;
using Anduril.Integrations;
using Anduril.Skills;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Services;

/// <summary>
/// Generates and emails recurring weekly menus from saved user preferences.
/// </summary>
public sealed class WeeklyMenuPlannerSchedulerService(
    IOptions<WeeklyMenuPlannerOptions> options,
    IWeeklyMenuSubscriptionStore subscriptionStore,
    PromptSkillRunner promptRunner,
    GmailTool gmailTool,
    ILogger<WeeklyMenuPlannerSchedulerService> logger) : BackgroundService
{
    private const string SkillName = "Weekly Menu Planner";
    private readonly WeeklyMenuPlannerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Weekly menu planner scheduler is disabled.");
            return;
        }

        await promptRunner.RefreshSkillsAsync(stoppingToken);

        var pollInterval = TimeSpan.FromMinutes(Math.Clamp(_options.SchedulerPollingIntervalMinutes, 1, 60));
        logger.LogInformation(
            "Weekly menu planner scheduler started with polling interval {PollingInterval}",
            pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed while processing weekly menu subscriptions");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static bool IsDue(WeeklyMenuSubscription subscription, DateTime utcNow)
    {
        var latestScheduledOccurrence = GetLatestScheduledOccurrence(subscription.DeliverySchedule, utcNow);
        if (latestScheduledOccurrence is null)
            return false;

        return subscription.LastDeliveredAtUtc is null || subscription.LastDeliveredAtUtc < latestScheduledOccurrence.Value;
    }

    internal static DateTime? GetLatestScheduledOccurrence(string deliverySchedule, DateTime utcNow)
    {
        if (!StandupSchedulerService.TryParseCronSchedule(deliverySchedule, out int minute, out int hour, out DayOfWeek[] days))
            return null;

        for (int offset = 0; offset <= 7; offset++)
        {
            var candidate = utcNow.Date.AddDays(-offset).AddHours(hour).AddMinutes(minute);
            if (candidate > utcNow)
                continue;

            if (days.Contains(candidate.DayOfWeek))
                return candidate;
        }

        return null;
    }

    internal static DateTime GetMenuWeekStartDate(DateTime scheduledOccurrenceUtc)
    {
        if (scheduledOccurrenceUtc.DayOfWeek == DayOfWeek.Sunday)
            return scheduledOccurrenceUtc.Date.AddDays(1);

        int daysSinceMonday = ((int)scheduledOccurrenceUtc.DayOfWeek + 6) % 7;
        return scheduledOccurrenceUtc.Date.AddDays(-daysSinceMonday);
    }

    internal static string BuildEmailSubject(DateTime weekStartDate)
        => $"Weekly menu for week of {weekStartDate:yyyy-MM-dd}";

    private async Task ProcessDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionStore.ListRecurringAsync(cancellationToken);
        if (subscriptions.Count == 0)
            return;

        var utcNow = DateTime.UtcNow;
        var dueSubscriptions = subscriptions.Where(subscription => IsDue(subscription, utcNow)).ToList();
        if (dueSubscriptions.Count == 0)
            return;

        if (!gmailTool.IsAvailable)
        {
            logger.LogWarning(
                "Weekly menu planner has {Count} due subscription(s), but Gmail is unavailable.",
                dueSubscriptions.Count);
            return;
        }

        foreach (var subscription in dueSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await GenerateAndSendAsync(subscription, utcNow, cancellationToken);
        }
    }

    private async Task GenerateAndSendAsync(
        WeeklyMenuSubscription subscription,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.RecipientEmail))
        {
            logger.LogWarning(
                "Skipping weekly menu delivery for user '{UserId}' because no recipient email is configured.",
                subscription.UserId);
            return;
        }

        try
        {
            var latestOccurrence = GetLatestScheduledOccurrence(subscription.DeliverySchedule, utcNow) ?? utcNow;
            var weekStartDate = GetMenuWeekStartDate(latestOccurrence);
            var context = new SkillContext
            {
                Message = new IncomingMessage
                {
                    Id = $"weekly-menu-{subscription.UserId}-{utcNow:yyyyMMddHHmmss}",
                    Text = BuildScheduledMenuRequest(subscription, weekStartDate),
                    UserId = subscription.UserId,
                    ChannelId = subscription.UserId,
                    Platform = "scheduler"
                },
                UserId = subscription.UserId,
                ChannelId = subscription.UserId
            };

            var result = await promptRunner.ExecuteWithoutToolsAsync(SkillName, context, cancellationToken);
            if (!result.Success)
            {
                logger.LogError(
                    "Weekly menu prompt skill failed for user '{UserId}': {Error}",
                    subscription.UserId,
                    result.ErrorMessage);
                return;
            }

            var subject = BuildEmailSubject(weekStartDate);

            await gmailTool.SendEmailAsync(subscription.RecipientEmail, subject, result.Response);
            await subscriptionStore.MarkDeliveredAsync(subscription.UserId, utcNow, cancellationToken);

            logger.LogInformation(
                "Weekly menu email sent to '{RecipientEmail}' for user '{UserId}'",
                subscription.RecipientEmail,
                subscription.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate or send weekly menu for user '{UserId}'", subscription.UserId);
        }
    }

    private static string BuildScheduledMenuRequest(WeeklyMenuSubscription subscription, DateTime weekStartDate)
    {
        return $"""
            Generate a weekly menu for the 7-day period starting {weekStartDate:yyyy-MM-dd} using these saved preferences.

            Saved preference summary:
            {subscription.PreferenceSummary}

            Additional constraints:
            - Cook for {subscription.PeopleCount} people
            - Meal complexity: {subscription.MealComplexity}
            - Include a grouped shopping list: {(subscription.IncludeShoppingList ? "yes" : "no")}
            - This is a scheduled weekly email, so produce the finished menu directly
            - Tool invocation is disabled for this scheduled run
            - Do not ask follow-up questions
            - Do not save, update, or disable preferences in this response
            """;
    }
}
