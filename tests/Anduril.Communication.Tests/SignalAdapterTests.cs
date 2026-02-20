using Anduril.Communication;
using Anduril.Core.Communication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Anduril.Communication.SignalAdapter;

namespace Anduril.Communication.Tests;

public class SignalAdapterTests
{
    private static SignalAdapter CreateAdapter(
        string? phoneNumber = null,
        string? apiUrl = null,
        HttpClient? httpClient = null)
    {
        var options = Options.Create(new SignalAdapterOptions
        {
            PhoneNumber = phoneNumber,
            ApiUrl = apiUrl
        });
        return new SignalAdapter(options, NullLogger<SignalAdapter>.Instance, httpClient ?? new HttpClient());
    }

    [Test]
    public async Task Platform_IsSignal()
    {
        var adapter = CreateAdapter();

        await Assert.That(adapter.Platform).IsEqualTo("signal");
    }

    [Test]
    public async Task IsConnected_IsFalse_ByDefault()
    {
        var adapter = CreateAdapter();

        await Assert.That(adapter.IsConnected).IsFalse();
    }

    [Test]
    public async Task StartAsync_DoesNotConnect_WhenPhoneNumberMissing()
    {
        var adapter = CreateAdapter(phoneNumber: null, apiUrl: "http://localhost:8080");

        await adapter.StartAsync();

        await Assert.That(adapter.IsConnected).IsFalse();
    }

    [Test]
    public async Task StartAsync_DoesNotConnect_WhenApiUrlMissing()
    {
        var adapter = CreateAdapter(phoneNumber: "+1234567890", apiUrl: null);

        await adapter.StartAsync();

        await Assert.That(adapter.IsConnected).IsFalse();
    }

    [Test]
    public async Task HandleSignalMessageAsync_FiltersBotOwnMessages()
    {
        var adapter = CreateAdapter();
        adapter._botPhoneNumber = "+1234567890";

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+1234567890",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "My own message"
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNull();
    }

    [Test]
    public async Task HandleSignalMessageAsync_FiltersNonDataMessages()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        // Envelope without DataMessage (e.g., receipt or typing indicator)
        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = null
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNull();
    }

    [Test]
    public async Task HandleSignalMessageAsync_FiltersEmptyMessages()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = ""
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNull();
    }

    [Test]
    public async Task HandleSignalMessageAsync_RaisesMessageReceivedEvent()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "Hello Anduril"
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Text).IsEqualTo("Hello Anduril");
        await Assert.That(received.UserId).IsEqualTo("+9876543210");
        await Assert.That(received.Platform).IsEqualTo("signal");
        await Assert.That(received.Id).IsEqualTo("1234567890");
    }

    [Test]
    public async Task HandleSignalMessageAsync_SetsDirectMessage_WhenNoGroup()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "DM text"
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.IsDirectMessage).IsTrue();
        await Assert.That(received.ChannelId).IsEqualTo("+9876543210");
    }

    [Test]
    public async Task HandleSignalMessageAsync_SetsGroupChannel_WhenGroupMessage()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "Group message",
                GroupInfo = new SignalGroupInfo { GroupId = "group-abc-123" }
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.IsDirectMessage).IsFalse();
        await Assert.That(received.ChannelId).IsEqualTo("group-abc-123");
    }

    [Test]
    public async Task HandleSignalMessageAsync_SetsThreadId_WhenQuotePresent()
    {
        var adapter = CreateAdapter();

        IncomingMessage? received = null;
        adapter.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "Reply to thread",
                Quote = new SignalQuote { Id = 1111111111 }
            }
        };

        await adapter.HandleSignalMessageAsync(envelope);

        await Assert.That(received).IsNotNull();
        await Assert.That(received!.ThreadId).IsEqualTo("1111111111");
    }

    [Test]
    public async Task HandleSignalMessageAsync_NoHandler_DoesNotThrow()
    {
        var adapter = CreateAdapter();

        var envelope = new SignalMessageEnvelope
        {
            SourceNumber = "+9876543210",
            DataMessage = new SignalDataMessage
            {
                Timestamp = 1234567890,
                Message = "No handler"
            }
        };

        // Should complete without throwing — default handler is a no-op
        await adapter.HandleSignalMessageAsync(envelope);
    }

    [Test]
    public async Task SendMessageAsync_Throws_WhenNotConnected()
    {
        var adapter = CreateAdapter();

        var message = new OutgoingMessage
        {
            Text = "Hello",
            ChannelId = "+9876543210"
        };

        await Assert.That(() => adapter.SendMessageAsync(message))
            .ThrowsException()
            .WithMessageMatching("*not connected*");
    }

    [Test]
    public async Task DisposeAsync_SafeWhenNotStarted()
    {
        var adapter = CreateAdapter();

        // Should not throw
        await adapter.DisposeAsync();

        await Assert.That(adapter.IsConnected).IsFalse();
    }


}
