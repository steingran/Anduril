using System.Text.Json;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Tests;

public class MessageProcessingServiceTests
{
    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Test]
    public async Task OnMessage_SendsPlaceholderThenUpdatesWithResponse()
    {
        var (service, adapter, _) = CreateService();
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        await Assert.That(adapter.SentMessages[0].Text).IsEqualTo("_⏳ Working on it…_");
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.UpdatedMessages[0].Message.Text).IsEqualTo("Test AI response");
    }

    [Test]
    public async Task OnMessage_UsesMessageThreadId_WhenPresent()
    {
        var (service, adapter, _) = CreateService();
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello", threadId: "thread-99"));

        await Assert.That(adapter.SentMessages[0].ThreadId).IsEqualTo("thread-99");
        await Assert.That(adapter.UpdatedMessages[0].Message.ThreadId).IsEqualTo("thread-99");
    }

    [Test]
    public async Task OnMessage_FallsBackToMessageId_WhenNoThreadId()
    {
        var (service, adapter, _) = CreateService();
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        await Assert.That(adapter.SentMessages[0].ThreadId).IsEqualTo("msg-1");
    }

    [Test]
    public async Task OnSlackMessage_FallbackPromptIncludesSlackConversationContext()
    {
        var provider = new FakeAiProvider("Test AI response");
        var (service, adapter, _) = CreateService(provider: provider);
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage(
            text: "find links in this thread",
            threadId: null,
            platform: "slack",
            channelId: "C12345",
            isDirectMessage: true));

        await Assert.That(provider.CapturedMessagesJson).IsNotNull();
        await Assert.That(provider.CapturedMessagesJson!).Contains("Current Slack conversation context");
        await Assert.That(provider.CapturedMessagesJson!).Contains("C12345");
        await Assert.That(provider.CapturedMessagesJson!).Contains("msg-1");
        await Assert.That(provider.CapturedMessagesJson!).Contains("Direct message: yes");
    }

    [Test]
    public async Task OnMessage_UpdateTargetsPlaceholderMessageId()
    {
        var (service, adapter, _) = CreateService();
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        await Assert.That(adapter.UpdatedMessages[0].MessageId).IsEqualTo("ts-1");
    }

    // ---------------------------------------------------------------
    // Placeholder send fails
    // ---------------------------------------------------------------

    [Test]
    public async Task OnMessage_PlaceholderFails_SendsFinalResponseAsNewMessage()
    {
        var (service, adapter, _) = CreateService();
        adapter.SendOverride = msg => msg.Text.Contains("⏳")
            ? throw new Exception("Placeholder failed")
            : Task.FromResult<string?>("ts-fallback");
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // Only the final response was recorded (placeholder threw before recording)
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.SentMessages[0].Text).IsEqualTo("Test AI response");
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OnMessage_PlaceholderReturnsNull_SendsFinalResponseAsNewMessage()
    {
        var (service, adapter, _) = CreateService();
        adapter.SendOverride = _ => Task.FromResult<string?>(null);
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // Placeholder sent but returned null, so response sent as new message (not update)
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(2);
        await Assert.That(adapter.SentMessages[1].Text).IsEqualTo("Test AI response");
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Update fails
    // ---------------------------------------------------------------

    [Test]
    public async Task OnMessage_UpdateFails_FallsBackToNewMessage()
    {
        var (service, adapter, _) = CreateService();
        adapter.UpdateOverride = (_, _) => throw new Exception("Update failed");
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // Placeholder + fallback response both sent as new messages
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(2);
        await Assert.That(adapter.SentMessages[0].Text).IsEqualTo("_⏳ Working on it…_");
        await Assert.That(adapter.SentMessages[1].Text).IsEqualTo("Test AI response");
        // Update was attempted but threw before being recorded
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Processing fails (router throws)
    // ---------------------------------------------------------------

    [Test]
    public async Task OnMessage_ProcessingFails_UpdatesPlaceholderWithErrorText()
    {
        var (service, adapter, _) = CreateService(router: new ThrowingRouter());
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // Placeholder was sent, then updated with error text
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.UpdatedMessages[0].MessageId).IsEqualTo("ts-1");
        await Assert.That(adapter.UpdatedMessages[0].Message.Text)
            .IsEqualTo("Sorry, something went wrong while processing your message.");
    }

    [Test]
    public async Task OnMessage_ProcessingFailsAndUpdateFails_SendsNewErrorMessage()
    {
        var (service, adapter, _) = CreateService(router: new ThrowingRouter());
        adapter.UpdateOverride = (_, _) => throw new Exception("Update also failed");
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // Placeholder sent + error sent as new message (update failed)
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(2);
        await Assert.That(adapter.SentMessages[1].Text)
            .IsEqualTo("Sorry, something went wrong while processing your message.");
    }

    [Test]
    public async Task OnMessage_ProcessingFailsNoPlaceholder_SendsNewErrorMessage()
    {
        var (service, adapter, _) = CreateService(router: new ThrowingRouter());
        adapter.SendOverride = msg => msg.Text.Contains("⏳")
            ? throw new Exception("Placeholder failed")
            : Task.FromResult<string?>("ts-err");
        await service.StartAsync(CancellationToken.None);
        await adapter.SimulateMessage(CreateMessage("hello"));

        // No placeholder → error sent as new message
        await Assert.That(adapter.SentMessages.Count).IsEqualTo(1);
        await Assert.That(adapter.SentMessages[0].Text)
            .IsEqualTo("Sorry, something went wrong while processing your message.");
        await Assert.That(adapter.UpdatedMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task StartAsync_DoesNotLogAdapterStarted_WhenAdapterDidNotConnect()
    {
        var logger = new ListLogger<MessageProcessingService>();
        var (service, _, _) = CreateService(logger: logger, connectAdapterOnStart: false);

        await service.StartAsync(CancellationToken.None);

        int startedLogCount = logger.Messages.Count(message =>
            message.Contains("Communication adapter 'test' started", StringComparison.Ordinal));
        await Assert.That(startedLogCount).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IncomingMessage CreateMessage(
        string text,
        string? threadId = null,
        string platform = "test",
        string channelId = "ch-1",
        bool isDirectMessage = false) => new()
    {
        Id = "msg-1", Text = text, UserId = "user-1",
        ChannelId = channelId, Platform = platform, ThreadId = threadId, IsDirectMessage = isDirectMessage
    };

    private static (MessageProcessingService Service, FakeAdapter Adapter, FakeAiProvider Provider) CreateService(
        FakeAiProvider? provider = null,
        ISkillRouter? router = null,
        ILogger<MessageProcessingService>? logger = null,
        bool connectAdapterOnStart = true)
    {
        var adapter = new FakeAdapter { ConnectOnStart = connectAdapterOnStart };
        provider ??= new FakeAiProvider("Test AI response");
        router ??= new SkillRouter(NullLogger<SkillRouter>.Instance);
        var loader = new PromptSkillLoader(NullLogger<PromptSkillLoader>.Instance);
        var promptRunner = new PromptSkillRunner(loader, [], NullLogger<PromptSkillRunner>.Instance, "nonexistent_skills");
        var compiledRunner = new CompiledSkillRunner(NullLogger<CompiledSkillRunner>.Instance, "nonexistent_plugins");
        var sessionStore = new FakeSessionStore();
        var sessionOptions = Options.Create(new ConversationSessionOptions());
        var service = new MessageProcessingService(
            [provider], Array.Empty<IIntegrationTool>(), [adapter], router,
            promptRunner, compiledRunner, Array.Empty<ISkill>(),
            sessionStore, sessionOptions,
            logger ?? NullLogger<MessageProcessingService>.Instance);
        return (service, adapter, provider);
    }

    // ---------------------------------------------------------------
    // Fakes
    // ---------------------------------------------------------------

    private sealed class FakeAdapter : ICommunicationAdapter
    {
        public string Platform => "test";
        public bool IsConnected { get; private set; }
        public bool ConnectOnStart { get; set; } = true;
        public event Func<IncomingMessage, Task> MessageReceived = _ => Task.CompletedTask;
        public List<OutgoingMessage> SentMessages { get; } = [];
        public List<(string MessageId, OutgoingMessage Message)> UpdatedMessages { get; } = [];
        public Func<OutgoingMessage, Task<string?>>? SendOverride { get; set; }
        public Func<string, OutgoingMessage, Task>? UpdateOverride { get; set; }
        private int _messageCounter;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = ConnectOnStart;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) { IsConnected = false; return Task.CompletedTask; }

        public async Task<string?> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
        {
            if (SendOverride is not null)
            {
                var result = await SendOverride(message);
                SentMessages.Add(message);
                return result;
            }
            SentMessages.Add(message);
            return $"ts-{Interlocked.Increment(ref _messageCounter)}";
        }

        public async Task UpdateMessageAsync(string messageId, OutgoingMessage message, CancellationToken cancellationToken = default)
        {
            if (UpdateOverride is not null) await UpdateOverride(messageId, message);
            UpdatedMessages.Add((messageId, message));
        }

        public Task SimulateMessage(IncomingMessage message) => MessageReceived(message);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAiProvider(string responseText) : IAiProvider
    {
        private readonly FakeChatClient _chatClient = new(responseText);

        public string Name => "fake-ai";
        public bool IsAvailable { get; private set; }
        public bool SupportsChatCompletion => true;
        public IChatClient ChatClient => _chatClient;
        public string? CapturedMessagesJson => _chatClient.CapturedMessagesJson;
        public Task InitializeAsync(CancellationToken cancellationToken = default) { IsAvailable = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AITool>>([]);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeChatClient(string responseText) : IChatClient
    {
        public string? CapturedMessagesJson { get; private set; }
        public ChatClientMetadata Metadata => new();
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CapturedMessagesJson = JsonSerializer.Serialize(chatMessages.ToList());
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class FakeSessionStore : IConversationSessionStore
    {
        public List<(string Key, SessionMessage Message)> AppendedMessages { get; } = [];

        public Task<IReadOnlyList<SessionMessage>> LoadAsync(string sessionKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SessionMessage>>([]);

        public Task AppendAsync(string sessionKey, SessionMessage message, CancellationToken cancellationToken = default)
        {
            AppendedMessages.Add((sessionKey, message));
            return Task.CompletedTask;
        }

        public Task ReplaceAllAsync(string sessionKey, IReadOnlyList<SessionMessage> messages, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingRouter : ISkillRouter
    {
        public Task<SkillInfo?> RouteAsync(IncomingMessage message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Router exploded");
        public void RegisterRunner(ISkillRunner runner) { }
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

