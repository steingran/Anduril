using Anduril.Host.Services;

namespace Anduril.Host.Tests;

public class StandupSchedulerServiceTests
{
    // ---------------------------------------------------------------
    // TryParseCronSchedule tests
    // ---------------------------------------------------------------

    [Test]
    public async Task TryParseCronSchedule_ValidMondayWednesday_ReturnsTrue()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "25 9 * * 1,3", out int minute, out int hour, out DayOfWeek[] days);

        await Assert.That(result).IsTrue();
        await Assert.That(minute).IsEqualTo(25);
        await Assert.That(hour).IsEqualTo(9);
        await Assert.That(days.Length).IsEqualTo(2);
        await Assert.That(days).Contains(DayOfWeek.Monday);
        await Assert.That(days).Contains(DayOfWeek.Wednesday);
    }

    [Test]
    public async Task TryParseCronSchedule_EveryWeekday_ReturnsAllFiveDays()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "0 8 * * 1,2,3,4,5", out _, out _, out DayOfWeek[] days);

        await Assert.That(result).IsTrue();
        await Assert.That(days.Length).IsEqualTo(5);
    }

    [Test]
    public async Task TryParseCronSchedule_SingleDay_ReturnsSingleDay()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "30 14 * * 5", out int minute, out int hour, out DayOfWeek[] days);

        await Assert.That(result).IsTrue();
        await Assert.That(minute).IsEqualTo(30);
        await Assert.That(hour).IsEqualTo(14);
        await Assert.That(days.Length).IsEqualTo(1);
        await Assert.That(days).Contains(DayOfWeek.Friday);
    }

    [Test]
    public async Task TryParseCronSchedule_Sunday_ReturnsDayOfWeekSunday()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "0 10 * * 0", out _, out _, out DayOfWeek[] days);

        await Assert.That(result).IsTrue();
        await Assert.That(days).Contains(DayOfWeek.Sunday);
    }

    [Test]
    public async Task TryParseCronSchedule_TooFewParts_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "25 9 * *", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_InvalidMinute_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "60 9 * * 1", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_InvalidHour_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "25 24 * * 1", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_InvalidDayOfWeek_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "25 9 * * 7", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_NonNumericMinute_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "abc 9 * * 1", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_EmptyString_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryParseCronSchedule_NegativeMinute_ReturnsFalse()
    {
        bool result = StandupSchedulerService.TryParseCronSchedule(
            "-1 9 * * 1", out _, out _, out _);

        await Assert.That(result).IsFalse();
    }

    // ---------------------------------------------------------------
    // CalculateNextRun tests
    // ---------------------------------------------------------------

    [Test]
    public async Task CalculateNextRun_BeforeScheduledTime_ReturnsSameDay()
    {
        // Monday 2026-02-16 08:00, schedule Mon+Wed at 09:25
        var now = new DateTime(2026, 2, 16, 8, 0, 0);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };

        var next = StandupSchedulerService.CalculateNextRun(now, 9, 25, days);

        await Assert.That(next).IsEqualTo(new DateTime(2026, 2, 16, 9, 25, 0));
    }

    [Test]
    public async Task CalculateNextRun_AfterScheduledTime_ReturnsNextScheduledDay()
    {
        // Monday 2026-02-16 10:00, schedule Mon+Wed at 09:25 → next is Wed
        var now = new DateTime(2026, 2, 16, 10, 0, 0);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };

        var next = StandupSchedulerService.CalculateNextRun(now, 9, 25, days);

        await Assert.That(next).IsEqualTo(new DateTime(2026, 2, 18, 9, 25, 0));
    }

    [Test]
    public async Task CalculateNextRun_OnWednesdayAfterTime_ReturnsNextMonday()
    {
        // Wednesday 2026-02-18 10:00, schedule Mon+Wed at 09:25 → next Mon
        var now = new DateTime(2026, 2, 18, 10, 0, 0);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };

        var next = StandupSchedulerService.CalculateNextRun(now, 9, 25, days);

        await Assert.That(next).IsEqualTo(new DateTime(2026, 2, 23, 9, 25, 0));
    }
}

