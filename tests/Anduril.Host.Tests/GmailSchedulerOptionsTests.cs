namespace Anduril.Host.Tests;

public class GmailSchedulerOptionsTests
{
    [Test]
    public async Task DefaultEnabled_IsFalse()
    {
        var options = new GmailSchedulerOptions();
        await Assert.That(options.Enabled).IsFalse();
    }

    [Test]
    public async Task DefaultSchedule_IsWeekdaysAt0700()
    {
        var options = new GmailSchedulerOptions();
        await Assert.That(options.Schedule).IsEqualTo("0 7 * * 1,2,3,4,5");
    }

    [Test]
    public async Task DefaultTargetPlatform_IsSlack()
    {
        var options = new GmailSchedulerOptions();
        await Assert.That(options.TargetPlatform).IsEqualTo("slack");
    }

    [Test]
    public async Task DefaultTargetChannel_IsNull()
    {
        var options = new GmailSchedulerOptions();
        await Assert.That(options.TargetChannel).IsNull();
    }

    [Test]
    public async Task DefaultSummaryHours_Is12()
    {
        var options = new GmailSchedulerOptions();
        await Assert.That(options.SummaryHours).IsEqualTo(12);
    }

    [Test]
    public async Task Enabled_CanBeSetToTrue()
    {
        var options = new GmailSchedulerOptions { Enabled = true };
        await Assert.That(options.Enabled).IsTrue();
    }

    [Test]
    public async Task Schedule_CanBeSet()
    {
        var options = new GmailSchedulerOptions { Schedule = "0 8 * * 1,2,3,4,5" };
        await Assert.That(options.Schedule).IsEqualTo("0 8 * * 1,2,3,4,5");
    }

    [Test]
    public async Task SummaryHours_CanBeSet()
    {
        var options = new GmailSchedulerOptions { SummaryHours = 24 };
        await Assert.That(options.SummaryHours).IsEqualTo(24);
    }
}

