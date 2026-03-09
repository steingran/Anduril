namespace Anduril.Host.Tests;

public class SentryBugfixOptionsTests
{
    [Test]
    public async Task DefaultEnabled_IsFalse()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.Enabled).IsFalse();
    }

    [Test]
    public async Task DefaultOccurrenceThreshold_Is10()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.OccurrenceThreshold).IsEqualTo(10);
    }

    [Test]
    public async Task DefaultNotificationPlatform_IsSlack()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.NotificationPlatform).IsEqualTo("slack");
    }

    [Test]
    public async Task DefaultNotificationChannel_IsNull()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.NotificationChannel).IsNull();
    }

    [Test]
    public async Task DefaultGitHubOwner_IsNull()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.GitHubOwner).IsNull();
    }

    [Test]
    public async Task DefaultGitHubRepo_IsNull()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.GitHubRepo).IsNull();
    }

    [Test]
    public async Task DefaultAugmentCliPath_IsAuggie()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.AugmentCliPath).IsEqualTo("auggie");
    }

    [Test]
    public async Task DefaultBranchPrefix_IsSentryBugfix()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.BranchPrefix).IsEqualTo("sentry-bugfix/");
    }

    [Test]
    public async Task DefaultAuggieTimeoutMinutes_Is10()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.AuggieTimeoutMinutes).IsEqualTo(10);
    }

    [Test]
    public async Task DefaultBaseBranch_IsMain()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.BaseBranch).IsEqualTo("main");
    }

    [Test]
    public async Task DefaultVerificationCommands_IsEmpty()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.VerificationCommands.Length).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultVerificationTimeoutMinutes_Is10()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.VerificationTimeoutMinutes).IsEqualTo(10);
    }

    [Test]
    public async Task DefaultWebhookSecret_IsNull()
    {
        var options = new SentryBugfixOptions();
        await Assert.That(options.WebhookSecret).IsNull();
    }
}

