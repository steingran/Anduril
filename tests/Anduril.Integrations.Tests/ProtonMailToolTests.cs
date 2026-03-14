using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class ProtonMailToolTests
{
    [Test]
    public async Task InitializeAsync_WhenDisabled_RemainsUnavailable()
    {
        var imapClient = new FakeProtonMailImapClient();
        var smtpClient = new FakeProtonMailSmtpClient();
        var tool = CreateTool(imapClient, smtpClient, configure: options => options.Enabled = false);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(imapClient.VerifyConnectionCallCount).IsEqualTo(0);
        await Assert.That(smtpClient.VerifyConnectionCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task InitializeAsync_WhenUsernameMissing_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<ProtonMailTool>();
        var tool = CreateTool(
            new FakeProtonMailImapClient(),
            new FakeProtonMailSmtpClient(),
            configure: options => options.Username = null,
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Any(message => message.Contains("Username", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenPasswordMissing_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<ProtonMailTool>();
        var tool = CreateTool(
            new FakeProtonMailImapClient(),
            new FakeProtonMailSmtpClient(),
            configure: options => options.Password = null,
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Any(message => message.Contains("Password", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenBothClientsVerify_BecomesAvailable()
    {
        var imapClient = new FakeProtonMailImapClient();
        var smtpClient = new FakeProtonMailSmtpClient();
        var tool = CreateTool(imapClient, smtpClient);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsTrue();
        await Assert.That(imapClient.VerifyConnectionCallCount).IsEqualTo(1);
        await Assert.That(smtpClient.VerifyConnectionCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task InitializeAsync_WhenImapVerificationFails_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<ProtonMailTool>();
        var tool = CreateTool(
            new FakeProtonMailImapClient { VerifyConnectionException = new InvalidOperationException("IMAP down") },
            new FakeProtonMailSmtpClient(),
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetFunctions_ReturnsExpectedFunctions()
    {
        var tool = CreateTool(new FakeProtonMailImapClient(), new FakeProtonMailSmtpClient());
        var names = tool.GetFunctions().Select(function => function.Name).ToList();

        await Assert.That(names.Count).IsEqualTo(7);
        await Assert.That(names).Contains("protonmail_list_messages");
        await Assert.That(names).Contains("protonmail_get_message");
        await Assert.That(names).Contains("protonmail_search");
        await Assert.That(names).Contains("protonmail_send");
        await Assert.That(names).Contains("protonmail_reply");
        await Assert.That(names).Contains("protonmail_move_message");
        await Assert.That(names).Contains("protonmail_set_read_status");
    }

    [Test]
    public async Task ListMessagesFunction_FormatsReturnedMessages()
    {
        var imapClient = new FakeProtonMailImapClient
        {
            ListMessagesResult =
            [
                CreateMessage(uid: 42, subject: "Status update", preview: "Bridge is healthy.")
            ]
        };
        var tool = CreateTool(imapClient, new FakeProtonMailSmtpClient());
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_list_messages", new Dictionary<string, object?>
        {
            ["maxResults"] = 5,
            ["mailbox"] = "INBOX"
        });

        await Assert.That(result).Contains("[42]");
        await Assert.That(result).Contains("Status update");
        await Assert.That(result).Contains("Bridge is healthy");
        await Assert.That(imapClient.LastListedMailbox).IsEqualTo("INBOX");
        await Assert.That(imapClient.LastListMaxResults).IsEqualTo(5);
    }

    [Test]
    public async Task SearchFunction_PassesQueryToImapClient()
    {
        var imapClient = new FakeProtonMailImapClient
        {
            SearchMessagesResult =
            [
                CreateMessage(uid: 7, subject: "Release", preview: "from:alice")
            ]
        };
        var tool = CreateTool(imapClient, new FakeProtonMailSmtpClient());
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_search", new Dictionary<string, object?>
        {
            ["query"] = "from:alice subject:release",
            ["maxResults"] = 3,
            ["mailbox"] = "Archive"
        });

        await Assert.That(result).Contains("Release");
        await Assert.That(imapClient.LastSearchQuery).IsEqualTo("from:alice subject:release");
        await Assert.That(imapClient.LastSearchedMailbox).IsEqualTo("Archive");
        await Assert.That(imapClient.LastSearchMaxResults).IsEqualTo(3);
    }

    [Test]
    public async Task SendFunction_UsesSmtpClient()
    {
        var smtpClient = new FakeProtonMailSmtpClient();
        var tool = CreateTool(new FakeProtonMailImapClient(), smtpClient);
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_send", new Dictionary<string, object?>
        {
            ["to"] = "alice@example.com",
            ["subject"] = "Hello",
            ["body"] = "Testing",
            ["cc"] = "bob@example.com"
        });

        await Assert.That(result).Contains("alice@example.com");
        await Assert.That(smtpClient.SentMessages.Count).IsEqualTo(1);
        await Assert.That(smtpClient.SentMessages[0].Subject).IsEqualTo("Hello");
        await Assert.That(smtpClient.SentMessages[0].Cc).IsEqualTo("bob@example.com");
    }

    [Test]
    public async Task ReplyFunction_UsesOriginalMessageMetadata()
    {
        var imapClient = new FakeProtonMailImapClient
        {
            GetMessageResult = CreateMessage(
                uid: 100,
                subject: "Need input",
                preview: "Please review.",
                from: "alice@example.com",
                replyTo: "reply@example.com",
                internetMessageId: "<abc@example.com>")
        };
        var smtpClient = new FakeProtonMailSmtpClient();
        var tool = CreateTool(imapClient, smtpClient);
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_reply", new Dictionary<string, object?>
        {
            ["messageUid"] = 100u,
            ["body"] = "Thanks!",
            ["mailbox"] = "INBOX"
        });

        await Assert.That(result).Contains("reply@example.com");
        await Assert.That(smtpClient.SentMessages.Count).IsEqualTo(1);
        await Assert.That(smtpClient.SentMessages[0].To).IsEqualTo("reply@example.com");
        await Assert.That(smtpClient.SentMessages[0].Subject).IsEqualTo("Re: Need input");
        await Assert.That(smtpClient.SentMessages[0].InReplyTo).IsEqualTo("<abc@example.com>");
    }

    [Test]
    public async Task MoveMessageFunction_UsesProvidedMailboxes()
    {
        var imapClient = new FakeProtonMailImapClient();
        var tool = CreateTool(imapClient, new FakeProtonMailSmtpClient());
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_move_message", new Dictionary<string, object?>
        {
            ["messageUid"] = 55u,
            ["destinationMailbox"] = "Archive",
            ["sourceMailbox"] = "INBOX"
        });

        await Assert.That(result).Contains("Archive");
        await Assert.That(imapClient.MovedMessages.Count).IsEqualTo(1);
        await Assert.That(imapClient.MovedMessages[0]).IsEqualTo((55u, "INBOX", "Archive"));
    }

    [Test]
    public async Task SetReadStatusFunction_UsesProvidedState()
    {
        var imapClient = new FakeProtonMailImapClient();
        var tool = CreateTool(imapClient, new FakeProtonMailSmtpClient());
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "protonmail_set_read_status", new Dictionary<string, object?>
        {
            ["messageUid"] = 77u,
            ["isRead"] = true,
            ["mailbox"] = "INBOX"
        });

        await Assert.That(result).Contains("read");
        await Assert.That(imapClient.ReadStatusUpdates.Count).IsEqualTo(1);
        await Assert.That(imapClient.ReadStatusUpdates[0]).IsEqualTo((77u, "INBOX", true));
    }

    private static ProtonMailTool CreateTool(
        FakeProtonMailImapClient imapClient,
        FakeProtonMailSmtpClient smtpClient,
        Action<ProtonMailToolOptions>? configure = null,
        TestListLogger<ProtonMailTool>? logger = null)
    {
        var options = new ProtonMailToolOptions
        {
            Enabled = true,
            Username = "user@pm.me",
            Password = "bridge-password",
            ImapHost = "localhost",
            ImapPort = 1143,
            SmtpHost = "localhost",
            SmtpPort = 1025,
            UseSsl = false
        };

        configure?.Invoke(options);

        return new ProtonMailTool(
            Options.Create(options),
            logger ?? new TestListLogger<ProtonMailTool>(),
            () => imapClient,
            () => smtpClient);
    }

    private static async Task<string> InvokeFunctionAsync(
        ProtonMailTool tool,
        string functionName,
        IDictionary<string, object?> arguments)
    {
        var function = tool.GetFunctions().First(function => function.Name.Equals(functionName, StringComparison.Ordinal));
        var result = await function.InvokeAsync(new AIFunctionArguments(arguments), CancellationToken.None);
        return result?.ToString() ?? string.Empty;
    }

    private static ProtonMailMessage CreateMessage(
        uint uid,
        string subject,
        string preview,
        string from = "alice@example.com",
        string replyTo = "alice@example.com",
        string to = "user@pm.me",
        string body = "Hello from Proton Mail.",
        string? internetMessageId = "<message@example.com>") =>
        new(
            uid,
            "INBOX",
            subject,
            from,
            replyTo,
            to,
            new DateTimeOffset(2026, 3, 11, 8, 30, 0, TimeSpan.Zero),
            preview,
            body,
            internetMessageId,
            false);
}
