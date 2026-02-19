namespace Anduril.Integrations.Tests;

public class GmailToolOptionsTests
{
    [Test]
    public async Task DefaultUserId_IsMe()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.UserId).IsEqualTo("me");
    }

    [Test]
    public async Task DefaultClientId_IsNull()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.ClientId).IsNull();
    }

    [Test]
    public async Task DefaultClientSecret_IsNull()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.ClientSecret).IsNull();
    }

    [Test]
    public async Task DefaultRefreshToken_IsNull()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.RefreshToken).IsNull();
    }

    [Test]
    public async Task DefaultPubSubTopic_IsNull()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.PubSubTopic).IsNull();
    }

    [Test]
    public async Task DefaultAttachmentSavePath_IsNull()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.AttachmentSavePath).IsNull();
    }

    [Test]
    public async Task DefaultRules_IsEmpty()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.Rules.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DefaultImportantSenders_IsEmpty()
    {
        var options = new GmailToolOptions();
        await Assert.That(options.ImportantSenders.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AllProperties_CanBeSet()
    {
        var options = new GmailToolOptions
        {
            ClientId = "test-id",
            ClientSecret = "test-secret",
            RefreshToken = "test-token",
            UserId = "user@gmail.com",
            PubSubTopic = "projects/test/topics/gmail",
            AttachmentSavePath = "/tmp/attachments",
            ImportantSenders = ["boss@company.com"],
            Rules = [new GmailEmailRule { Name = "r1", Action = "notify" }]
        };

        await Assert.That(options.ClientId).IsEqualTo("test-id");
        await Assert.That(options.ClientSecret).IsEqualTo("test-secret");
        await Assert.That(options.RefreshToken).IsEqualTo("test-token");
        await Assert.That(options.UserId).IsEqualTo("user@gmail.com");
        await Assert.That(options.PubSubTopic).IsEqualTo("projects/test/topics/gmail");
        await Assert.That(options.AttachmentSavePath).IsEqualTo("/tmp/attachments");
        await Assert.That(options.ImportantSenders.Count).IsEqualTo(1);
        await Assert.That(options.Rules.Count).IsEqualTo(1);
    }
}

