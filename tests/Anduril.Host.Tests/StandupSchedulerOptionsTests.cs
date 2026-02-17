namespace Anduril.Host.Tests;

public class StandupSchedulerOptionsTests
{
    [Test]
    public async Task DefaultSchedule_IsMondayWednesdayAt0925()
    {
        var options = new StandupSchedulerOptions();

        await Assert.That(options.Schedule).IsEqualTo("25 9 * * 1,3");
    }

    [Test]
    public async Task DefaultTargetPlatform_IsSlack()
    {
        var options = new StandupSchedulerOptions();

        await Assert.That(options.TargetPlatform).IsEqualTo("slack");
    }

    [Test]
    public async Task DefaultTargetChannel_IsNull()
    {
        var options = new StandupSchedulerOptions();

        await Assert.That(options.TargetChannel).IsNull();
    }

    [Test]
    public async Task DefaultEnabled_IsFalse()
    {
        var options = new StandupSchedulerOptions();

        await Assert.That(options.Enabled).IsFalse();
    }

    [Test]
    public async Task Schedule_CanBeSet()
    {
        var options = new StandupSchedulerOptions { Schedule = "0 8 * * 1,2,3,4,5" };

        await Assert.That(options.Schedule).IsEqualTo("0 8 * * 1,2,3,4,5");
    }

    [Test]
    public async Task Enabled_CanBeSetToTrue()
    {
        var options = new StandupSchedulerOptions { Enabled = true };

        await Assert.That(options.Enabled).IsTrue();
    }
}

