using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.AI.Providers;

/// <summary>
/// Bridges a <see cref="CopilotClient"/> to the <see cref="IChatClient"/> abstraction
/// from Microsoft.Extensions.AI, enabling the GitHub Copilot SDK to be used as a
/// standard AI provider in Anduril.
/// </summary>
internal sealed class CopilotChatClientAdapter : IChatClient
{
    private readonly CopilotClient _client;
    private readonly string _model;
    private readonly ILogger? _logger;

    public CopilotChatClientAdapter(CopilotClient client, string model, ILogger? logger = null)
    {
        _client = client;
        _model = model;
        _logger = logger;
    }

    public ChatClientMetadata Metadata => new("copilot", null, _model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _model;
        var messageList = chatMessages.ToList();
        var systemPrompt = BuildSystemPromptWithHistory(messageList);
        var userPrompt = ExtractLastUserMessage(messageList);

        await using var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = false,
            // Disable infinite sessions: this adapter creates a new session per request
            // and disposes it immediately after. Infinite sessions are for long-lived reused
            // sessions; leaving them enabled causes the SDK to emit a spurious SessionIdleEvent
            // before response content arrives, which would complete the TCS with an empty string.
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = ApproveAllPermissions,
            SystemMessage = systemPrompt is not null
                ? new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = systemPrompt }
                : null,
        }, cancellationToken);

        var tcs = new TaskCompletionSource<string>();

        // Accumulates the actual response text (message events only).
        var contentBuilder = new System.Text.StringBuilder();

        // Accumulate message content and reasoning content separately.
        // Message events always take precedence; reasoning is a last-resort fallback for
        // older models that deliver no message events at all.
        var receivedMessageContent = false;
        var reasoningBuilder = new System.Text.StringBuilder();

        session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg when !string.IsNullOrWhiteSpace(msg.Data.Content):
                    // Primary response channel — accumulate and mark as received.
                    receivedMessageContent = true;
                    contentBuilder.Append(msg.Data.Content);
                    break;
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    receivedMessageContent = true;
                    contentBuilder.Append(delta.Data.DeltaContent);
                    break;
                case AssistantReasoningEvent reasoning when !string.IsNullOrEmpty(reasoning.Data.Content):
                    // Extended-thinking content. Accumulate only as fallback; ignored once
                    // actual message content has been received.
                    if (!receivedMessageContent)
                        reasoningBuilder.Append(reasoning.Data.Content);
                    break;
                case AssistantReasoningDeltaEvent reasoningDelta when !string.IsNullOrEmpty(reasoningDelta.Data.DeltaContent):
                    if (!receivedMessageContent)
                        reasoningBuilder.Append(reasoningDelta.Data.DeltaContent);
                    break;
                case SessionErrorEvent err:
                    tcs.TrySetException(new InvalidOperationException(
                        $"Copilot error ({err.Data.ErrorType}): {err.Data.Message}"));
                    break;
                case SessionIdleEvent:
                    // Session finished. Use reasoning as last-resort if no message content arrived.
                    if (!receivedMessageContent && reasoningBuilder.Length > 0)
                        contentBuilder.Append(reasoningBuilder);
                    tcs.TrySetResult(contentBuilder.ToString());
                    break;
                case AssistantTurnStartEvent:
                case AssistantTurnEndEvent:
                case AssistantUsageEvent:
                case AssistantIntentEvent:
                case UserMessageEvent:
                case PendingMessagesModifiedEvent:
                case SessionUsageInfoEvent:
                case SessionStartEvent:
                case SessionInfoEvent:
                // Copilot SDK agent-loop events: emitted when the model invokes internal tools
                // (e.g. when MEAI AIFunction tool definitions are passed and the Copilot SDK
                // tries to execute them through its own sub-agent infrastructure). These events
                // are informational — content accumulation and completion are still driven by
                // AssistantMessageEvent and SessionIdleEvent respectively.
                case ToolExecutionStartEvent:
                case ToolExecutionCompleteEvent:
                case SubagentStartedEvent:
                case SubagentCompletedEvent:
                    break;
                // Empty-content message/reasoning events — start/end markers used by some models
                // (e.g. claude-sonnet-4.6). Explicitly silenced so they don't reach the default
                // warning case. MUST be after all named specific types above so that those types
                // are matched by their own cases first, even if they inherit from these base types.
                case AssistantMessageEvent:
                case AssistantMessageDeltaEvent:
                case AssistantReasoningEvent:
                case AssistantReasoningDeltaEvent:
                    break;
                default:
                    _logger?.LogWarning(
                        "Copilot non-streaming session received unhandled event type '{EventType}' for model '{Model}'",
                        evt.GetType().Name, _model);
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = userPrompt }, cancellationToken);
        var content = await tcs.Task.WaitAsync(cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, content))
        {
            ModelId = model,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _model;
        var messageList = chatMessages.ToList();
        var systemPrompt = BuildSystemPromptWithHistory(messageList);
        var userPrompt = ExtractLastUserMessage(messageList);

        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleWriter = true });

        await using var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true,
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = ApproveAllPermissions,
            SystemMessage = systemPrompt is not null
                ? new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = systemPrompt }
                : null,
        }, cancellationToken);

        // AssistantMessageDeltaEvent is the primary streaming channel (actual response tokens).
        // AssistantReasoningDeltaEvent carries extended-thinking content (internal to the model)
        // and must NOT be streamed directly — it would show raw reasoning thoughts instead of the
        // answer. Reasoning is accumulated only as a last-resort fallback for models that never
        // emit any message events at all (older behaviour). AssistantMessageEvent contains the
        // full response for models that don't stream deltas and is emitted as a single chunk.
        var receivedMessageDelta = false;   // true once any AssistantMessageDeltaEvent arrives
        var receivedMessageContent = false; // true once any message event (delta or full) arrives
        var messageFallbackBuilder = new System.Text.StringBuilder();
        var reasoningFallbackBuilder = new System.Text.StringBuilder();

        session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    // Stream actual response tokens directly.
                    receivedMessageDelta = true;
                    receivedMessageContent = true;
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;
                case AssistantMessageEvent msg when !string.IsNullOrEmpty(msg.Data.Content):
                    // Full response event (non-streaming models or post-delta summary).
                    // Skip if deltas already streamed the content to avoid duplication.
                    receivedMessageContent = true;
                    if (!receivedMessageDelta)
                        messageFallbackBuilder.Append(msg.Data.Content);
                    break;
                case AssistantReasoningDeltaEvent reasoningDelta when !string.IsNullOrEmpty(reasoningDelta.Data.DeltaContent):
                    // Extended-thinking delta — internal reasoning, NOT the answer.
                    // Accumulate only as last-resort fallback (used when no message content arrives).
                    if (!receivedMessageContent)
                        reasoningFallbackBuilder.Append(reasoningDelta.Data.DeltaContent);
                    break;
                case AssistantReasoningEvent reasoning when !string.IsNullOrEmpty(reasoning.Data.Content):
                    if (!receivedMessageContent)
                        reasoningFallbackBuilder.Append(reasoning.Data.Content);
                    break;
                case SessionIdleEvent:
                    // Emit accumulated content for non-streaming models.
                    if (!receivedMessageDelta)
                    {
                        // Prefer actual message content; fall back to reasoning only if nothing else arrived.
                        var fallback = messageFallbackBuilder.Length > 0
                            ? messageFallbackBuilder.ToString()
                            : reasoningFallbackBuilder.ToString();
                        if (fallback.Length > 0)
                            channel.Writer.TryWrite(fallback);
                    }
                    channel.Writer.TryComplete();
                    break;
                case SessionErrorEvent err:
                    channel.Writer.TryComplete(new InvalidOperationException(
                        $"Copilot error ({err.Data.ErrorType}): {err.Data.Message}"));
                    break;
                case AssistantTurnStartEvent:
                case AssistantTurnEndEvent:
                case AssistantUsageEvent:
                case AssistantIntentEvent:
                case UserMessageEvent:
                case PendingMessagesModifiedEvent:
                case SessionUsageInfoEvent:
                case SessionStartEvent:
                case SessionInfoEvent:
                case ToolExecutionStartEvent:
                case ToolExecutionCompleteEvent:
                case SubagentStartedEvent:
                case SubagentCompletedEvent:
                    break;
                // Empty-content message/reasoning events — start/end markers used by some models
                // (e.g. claude-sonnet-4.6). Explicitly silenced so they don't reach the default
                // warning case. MUST be after all named specific types above so that those types
                // are matched by their own cases first, even if they inherit from these base types.
                case AssistantMessageEvent:
                case AssistantMessageDeltaEvent:
                case AssistantReasoningEvent:
                case AssistantReasoningDeltaEvent:
                    break;
                default:
                    _logger?.LogWarning(
                        "Copilot streaming session received unhandled event type '{EventType}' for model '{Model}'",
                        evt.GetType().Name, _model);
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = userPrompt }, cancellationToken);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk)
            {
                ModelId = model,
            };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(CopilotClient))
            return _client;

        return null;
    }

    public void Dispose()
    {
        // CopilotClient lifecycle is managed by CopilotProvider, not this adapter.
    }

    private static Task<PermissionRequestResult> ApproveAllPermissions(
        PermissionRequest request, PermissionInvocation invocation)
    {
        return Task.FromResult(new PermissionRequestResult { Kind = "allow" });
    }

    /// <summary>
    /// Builds the system prompt to pass to the Copilot session. Because the Copilot SDK is
    /// single-turn (one SendAsync per session), conversation history cannot be replayed as
    /// individual messages. Instead, prior turns are serialised into the system prompt so the
    /// model retains full context across messages within the same session key.
    /// </summary>
    private static string? BuildSystemPromptWithHistory(IReadOnlyList<ChatMessage> messages)
    {
        var systemText = string.Join("\n\n", messages
            .Where(m => m.Role == ChatRole.System)
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrEmpty(t)));

        // Collect all non-system messages except the last user message (the current query).
        var prior = messages
            .Where(m => m.Role != ChatRole.System)
            .SkipLast(1)
            .ToList();

        if (prior.Count == 0)
            return string.IsNullOrEmpty(systemText) ? null : systemText;

        var history = new System.Text.StringBuilder("\n\nConversation so far:\n");
        foreach (var msg in prior)
        {
            var role = msg.Role == ChatRole.Assistant ? "Assistant" : "User";
            history.AppendLine($"{role}: {msg.Text}");
        }

        return string.IsNullOrEmpty(systemText)
            ? history.ToString().TrimEnd()
            : systemText + history;
    }

    private static string ExtractLastUserMessage(IReadOnlyList<ChatMessage> messages)
    {
        return messages
            .LastOrDefault(m => m.Role == ChatRole.User)
            ?.Text ?? string.Empty;
    }
}
