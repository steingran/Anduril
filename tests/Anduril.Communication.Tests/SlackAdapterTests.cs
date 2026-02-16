using Anduril.Communication;
using Anduril.Core.Communication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlackNet.Events;

namespace Anduril.Communication.Tests;

public class SlackAdapterTests
{
    private static SlackAdapter CreateAdapter(string? botToken = null, string? appToken = null)
    {
        var options = Options.Create(new SlackAdapterOptions
        {
            BotToken = botToken,
            AppToken = appToken
        });
        return new SlackAdapter(options, NullLogger<SlackAdapter>.Instance);
    }

    [Test]
    public async Task Platform_IsSlack()
    {
        var adapter = CreateAdapter();

        await Assert.That(adapter.Platform).IsEqualTo("slack");
    }

    [Test]
    public async Task IsConnected_IsFalse_ByDefault()
    {
        var adapter = CreateAdapter();

        await Assert.That(adapter.IsConnected).IsFalse();
    }

    [Test]
    public async Task StartAsync_ThrowsWhenBotTokenMissing()
    {
        var adapter = CreateAdapter(botToken: null, appToken: "xapp-test");

        await Assert.That(() => adapter.StartAsync())
            .ThrowsException()
            .WithMessageMatching("*BotToken*");
    }

    [Test]
    public async Task StartAsync_ThrowsWhenAppTokenMissing()
    {
        var adapter = CreateAdapter(botToken: "xoxb-test", appToken: null);

        await Assert.That(() => adapter.StartAsync())
            .ThrowsException()
            .WithMessageMatching("*AppToken*");
    }

    [Test]
    public async Task HandleSlackMessageAsync_FiltersBotOwnMessages()
    {
        var adapter = CreateAdapter();
        // Use reflection to set the bot user ID since it's normally set during StartAsync
        SetBotUserId(adapter, "U_BOT");

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var slackMessage = new MessageEvent
        {
            User = "U_BOT",
            Text = "My own message",
            Ts = "1234567890.000001",
            Channel = "C_CHANNEL"
        };

        await adapter.HandleSlackMessageAsync(slackMessage);

        await Assert.That(received).IsNull();
    }

    [Test]
    public async Task HandleSlackMessageAsync_FiltersMessageSubtypes()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var slackMessage = new MessageEvent
        {
            User = "U_USER",
            Subtype = "message_changed",
            Text = "Edited text",
            Ts = "1234567890.000002",
            Channel = "C_CHANNEL"
        };

        await adapter.HandleSlackMessageAsync(slackMessage);

        await Assert.That(received).IsNull();
    }

    [Test]
    public async Task HandleSlackMessageAsync_RaisesMessageReceivedEvent()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var slackMessage = new MessageEvent
        {
            User = "U_USER",
            Text = "Hello Anduril",
            Ts = "1234567890.000003",
            Channel = "C_CHANNEL"
        };

        await adapter.HandleSlackMessageAsync(slackMessage);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Text).IsEqualTo("Hello Anduril");
        await Assert.That(received.UserId).IsEqualTo("U_USER");
        await Assert.That(received.ChannelId).IsEqualTo("C_CHANNEL");
        await Assert.That(received.Platform).IsEqualTo("slack");
        await Assert.That(received.Id).IsEqualTo("1234567890.000003");
    }

    [Test]
    public async Task HandleSlackMessageAsync_SetsThreadId()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var slackMessage = new MessageEvent
        {
            User = "U_USER",
            Text = "Thread reply",
            Ts = "1234567890.000004",
            ThreadTs = "1234567890.000001",
            Channel = "C_CHANNEL"
        };

        await adapter.HandleSlackMessageAsync(slackMessage);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.ThreadId).IsEqualTo("1234567890.000001");
    }

    [Test]
    public async Task HandleSlackMessageAsync_SetsDirectMessageFlag()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var slackMessage = new MessageEvent
        {
            User = "U_USER",
            Text = "DM text",
            Ts = "1234567890.000005",
            Channel = "D_DM",
            ChannelType = "im"
        };

        await adapter.HandleSlackMessageAsync(slackMessage);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.IsDirectMessage).IsTrue();
    }

    [Test]
    public async Task HandleSlackMessageAsync_NoEvent_DoesNotThrow()
    {
        var adapter = CreateAdapter();
        // No MessageReceived handler subscribed — should not throw

        var slackMessage = new MessageEvent
        {
            User = "U_USER",
            Text = "No handler",
            Ts = "1234567890.000006",
            Channel = "C_CHANNEL"
        };

        // Should complete without throwing
        await adapter.HandleSlackMessageAsync(slackMessage);
    }

    [Test]
    public async Task DisposeAsync_SafeWhenNotStarted()
    {
        var adapter = CreateAdapter();

        // Should not throw
        await adapter.DisposeAsync();

        await Assert.That(adapter.IsConnected).IsFalse();
    }

    /// <summary>
    /// Sets the internal _botUserId field for testing (accessible via InternalsVisibleTo).
    /// </summary>
    private static void SetBotUserId(SlackAdapter adapter, string botUserId)
    {
        adapter._botUserId = botUserId;
    }
}

