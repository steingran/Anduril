using Anduril.Core.MenuPlanning;
using Anduril.Host.Services;

namespace Anduril.Host.Tests;

public class WeeklyMenuPlannerSchedulerServiceTests
{
    [Test]
    public async Task IsDue_ReturnsTrue_WhenCurrentScheduleHasNotBeenDelivered()
    {
        var subscription = new WeeklyMenuSubscription
        {
            UserId = "user-1",
            RecipientEmail = "user@example.com",
            PreferenceSummary = "Vegetarian.",
            DeliverySchedule = "0 18 * * 0",
            IsRecurringEnabled = true,
            LastDeliveredAtUtc = new DateTime(2026, 03, 01, 18, 00, 00, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.UtcNow
        };

        bool due = WeeklyMenuPlannerSchedulerService.IsDue(
            subscription,
            new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc));

        await Assert.That(due).IsTrue();
    }

    [Test]
    public async Task IsDue_ReturnsFalse_WhenLatestOccurrenceWasAlreadyDelivered()
    {
        var deliveredAtUtc = new DateTime(2026, 03, 08, 18, 01, 00, DateTimeKind.Utc);
        var subscription = new WeeklyMenuSubscription
        {
            UserId = "user-2",
            RecipientEmail = "user@example.com",
            PreferenceSummary = "High protein.",
            DeliverySchedule = "0 18 * * 0",
            IsRecurringEnabled = true,
            LastDeliveredAtUtc = deliveredAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        bool due = WeeklyMenuPlannerSchedulerService.IsDue(
            subscription,
            new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc));

        await Assert.That(due).IsFalse();
    }

    [Test]
    public async Task GetLatestScheduledOccurrence_ReturnsNull_ForInvalidSchedule()
    {
        var latestOccurrence = WeeklyMenuPlannerSchedulerService.GetLatestScheduledOccurrence(
            "invalid schedule",
            new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc));

        await Assert.That(latestOccurrence).IsNull();
    }

    [Test]
    public async Task GetMenuWeekStartDate_ReturnsNextMonday_ForSundaySchedule()
    {
        var weekStart = WeeklyMenuPlannerSchedulerService.GetMenuWeekStartDate(
            new DateTime(2026, 03, 08, 18, 00, 00, DateTimeKind.Utc));

        await Assert.That(weekStart).IsEqualTo(new DateTime(2026, 03, 09, 00, 00, 00, DateTimeKind.Utc));
    }

    [Test]
    public async Task GetMenuWeekStartDate_ReturnsCurrentWeekMonday_ForMidweekSchedule()
    {
        var weekStart = WeeklyMenuPlannerSchedulerService.GetMenuWeekStartDate(
            new DateTime(2026, 03, 11, 18, 00, 00, DateTimeKind.Utc));

        await Assert.That(weekStart).IsEqualTo(new DateTime(2026, 03, 09, 00, 00, 00, DateTimeKind.Utc));
    }
}
