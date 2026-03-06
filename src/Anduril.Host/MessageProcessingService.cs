using System.Text.Json;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Anduril.Host;

/// <summary>
/// Background service that orchestrates the full message-processing pipeline:
/// initializes AI providers and integration tools, wires up skill routing, starts communication
/// adapters, and handles incoming messages end-to-end.
/// </summary>
public sealed class MessageProcessingService(
    IEnumerable<IAiProvider> aiProviders,
    IEnumerable<IIntegrationTool> integrationTools,
    IEnumerable<ICommunicationAdapter> adapters,
    ISkillRouter router,
    PromptSkillRunner promptRunner,
    CompiledSkillRunner compiledRunner,
    IEnumerable<ISkill> compiledSkills,
    IConversationSessionStore sessionStore,
    IOptions<ConversationSessionOptions> sessionOptions,
    ILogger<MessageProcessingService> logger)
    : IHostedService, IAsyncDisposable
{
    // Store event handlers for cleanup
    private readonly Dictionary<ICommunicationAdapter, Func<IncomingMessage, Task>> _eventHandlers = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 1. Initialize AI providers (best-effort — log failures, keep going)
        int initializedCount = 0;
        foreach (var provider in aiProviders)
        {
            try
            {
                await provider.InitializeAsync(cancellationToken);
                if (provider.IsAvailable)
                {
                    initializedCount++;
                    logger.LogInformation("AI provider '{Name}' initialized successfully", provider.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI provider '{Name}' failed to initialize — skipping", provider.Name);
            }
        }

        int chatCount = aiProviders.Count(p => p.IsAvailable && p.SupportsChatCompletion);
        int toolCount = aiProviders.Count(p => p.IsAvailable && !p.SupportsChatCompletion);
        logger.LogInformation(
            "{ChatCount} chat provider(s) and {ToolCount} tool-only provider(s) available",
            chatCount, toolCount);

        if (chatCount == 0)
        {
            logger.LogWarning(
                "No chat-capable AI provider is available. " +
                "Fallback AI chat will not work. Configure OpenAI, Anthropic, Augment Chat, Ollama, or LLamaSharp with valid credentials.");
        }

        // 2. Initialize integration tools (best-effort — log failures, keep going)
        int toolsInitialized = 0;
        foreach (var tool in integrationTools)
        {
            try
            {
                await tool.InitializeAsync(cancellationToken);
                if (tool.IsAvailable)
                {
                    toolsInitialized++;
                    logger.LogInformation("Integration tool '{Name}' initialized successfully", tool.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Integration tool '{Name}' failed to initialize — skipping", tool.Name);
            }
        }

        logger.LogInformation("{Count} integration tool(s) available", toolsInitialized);

        // 3. Register skill runners with the router and build the skill index
        compiledRunner.LoadFromDirectory();
        foreach (var skill in compiledSkills)
        {
            compiledRunner.Register(skill);
        }
        router.RegisterRunner(promptRunner);
        router.RegisterRunner(compiledRunner);
        await router.RefreshAsync(cancellationToken);

        // 4. Start communication adapters and subscribe to incoming messages
        foreach (var adapter in adapters)
        {
            try
            {
                // Store the handler so we can unsubscribe later
                Func<IncomingMessage, Task> handler = msg => OnMessageReceivedAsync(adapter, msg);
                _eventHandlers[adapter] = handler;
                adapter.MessageReceived += handler;

                await adapter.StartAsync(cancellationToken);
                if (adapter.IsConnected)
                {
                    logger.LogInformation("Communication adapter '{Platform}' started", adapter.Platform);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Communication adapter '{Platform}' failed to start — skipping", adapter.Platform);
            }
        }

        logger.LogInformation("Message processing pipeline is ready");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from events before stopping adapters
        foreach ((ICommunicationAdapter adapter, Func<IncomingMessage, Task> handler) in _eventHandlers)
        {
            try
            {
                adapter.MessageReceived -= handler;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error unsubscribing from adapter '{Platform}'", adapter.Platform);
            }
        }
        _eventHandlers.Clear();

        foreach (var adapter in adapters)
        {
            try
            {
                await adapter.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping adapter '{Platform}'", adapter.Platform);
            }
        }

        logger.LogInformation("Message processing pipeline stopped");
    }

    private async Task OnMessageReceivedAsync(ICommunicationAdapter sourceAdapter, IncomingMessage message)
    {
        string threadId = message.ThreadId ?? message.Id; // always reply in thread
        string? placeholderMessageId = null;

        try
        {
            logger.LogDebug(
                "Received message from {User} on {Platform}/{Channel}: {Text}",
                message.UserId, message.Platform, message.ChannelId,
                message.Text.Length > 80 ? message.Text[..80] + "…" : message.Text);

            // Post a placeholder message immediately so the user knows we're working on it
            try
            {
                placeholderMessageId = await sourceAdapter.SendMessageAsync(new OutgoingMessage
                {
                    Text = "_⏳ Working on it…_",
                    ChannelId = message.ChannelId,
                    ThreadId = threadId
                });
            }
            catch (Exception placeholderEx)
            {
                logger.LogWarning(placeholderEx, "Failed to send placeholder message — continuing without it");
            }

            // Route the message to a skill (or null for general conversation)
            var skill = await router.RouteAsync(message);
            string responseText;

            if (skill is not null)
            {
                responseText = await ExecuteSkillAsync(skill, message);
            }
            else
            {
                responseText = await FallbackAiChatAsync(message);
            }

            // Update the placeholder with the real response, or send a new message if no placeholder
            if (placeholderMessageId is not null)
            {
                try
                {
                    await sourceAdapter.UpdateMessageAsync(placeholderMessageId, new OutgoingMessage
                    {
                        Text = responseText,
                        ChannelId = message.ChannelId,
                        ThreadId = threadId
                    });
                }
                catch (Exception updateEx)
                {
                    // Update failed — fall back to sending a new message
                    logger.LogWarning(updateEx, "Failed to update placeholder message — sending new message instead");
                    await sourceAdapter.SendMessageAsync(new OutgoingMessage
                    {
                        Text = responseText,
                        ChannelId = message.ChannelId,
                        ThreadId = threadId
                    });
                }
            }
            else
            {
                await sourceAdapter.SendMessageAsync(new OutgoingMessage
                {
                    Text = responseText,
                    ChannelId = message.ChannelId,
                    ThreadId = threadId
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId}", message.Id);

            try
            {
                string errorText = "Sorry, something went wrong while processing your message.";

                // Try to update the placeholder with the error, otherwise send a new message
                if (placeholderMessageId is not null)
                {
                    try
                    {
                        await sourceAdapter.UpdateMessageAsync(placeholderMessageId, new OutgoingMessage
                        {
                            Text = errorText,
                            ChannelId = message.ChannelId,
                            ThreadId = threadId
                        });
                        return;
                    }
                    catch (Exception updateEx)
                    {
                        logger.LogWarning(updateEx, "Failed to update placeholder error message for message {MessageId}", message.Id);
                    }
                }

                await sourceAdapter.SendMessageAsync(new OutgoingMessage
                {
                    Text = errorText,
                    ChannelId = message.ChannelId,
                    ThreadId = threadId
                });
            }
            catch (Exception sendEx)
            {
                logger.LogError(sendEx, "Failed to send error response for message {MessageId}", message.Id);
            }
        }
    }

    private async Task<string> ExecuteSkillAsync(SkillInfo skill, IncomingMessage message)
    {
        // Find the runner that can handle this skill type
        ISkillRunner? runner = skill.SkillType switch
        {
            "prompt" => promptRunner,
            "compiled" => compiledRunner,
            _ => null
        };

        if (runner is null)
        {
            logger.LogWarning("No runner found for skill type '{Type}'", skill.SkillType);
            return $"I matched the *{skill.Name}* skill but don't know how to run it (unknown type '{skill.SkillType}').";
        }

        var context = new SkillContext
        {
            Message = message,
            UserId = message.UserId,
            ChannelId = message.ChannelId
        };

        var result = await runner.ExecuteAsync(skill.Name, context);

        if (result.Success)
        {
            return result.Response;
        }

        // Log the detailed error but return a generic message to the user
        logger.LogError("Skill '{SkillName}' failed: {ErrorMessage}", skill.Name, result.ErrorMessage);
        return $"Sorry, the *{skill.Name}* skill encountered an error. Please try again later.";
    }

    private async Task<string> FallbackAiChatAsync(IncomingMessage message)
    {
        var provider = aiProviders.FirstOrDefault(p => p.IsAvailable && p.SupportsChatCompletion);
        if (provider is null)
        {
            bool hasToolProviders = aiProviders.Any(p => p.IsAvailable && !p.SupportsChatCompletion);
            return hasToolProviders
                ? "No chat-capable AI provider is available. Tool providers (e.g., Augment MCP) are running but cannot handle direct conversation. Please configure OpenAI, Anthropic, Augment Chat, Ollama, or LLamaSharp."
                : "No AI provider is available right now. Please check the configuration.";
        }

        // Session key = platform:userId — gives each user a persistent conversation
        string sessionKey = $"{message.Platform}:{message.UserId}";

        try
        {
            var chatClient = provider.ChatClient;

            // Collect all available integration tool functions (Gmail, GitHub, Sentry, Calendar, etc.)
            var tools = new List<AITool>();
            foreach (var tool in integrationTools.Where(t => t.IsAvailable))
            {
                tools.AddRange(tool.GetFunctions());
            }

            // Also collect tools from AI providers (e.g., MCP server tools)
            foreach (var p in aiProviders.Where(p => p.IsAvailable))
            {
                try
                {
                    var providerTools = await p.GetToolsAsync();
                    tools.AddRange(providerTools);
                }
                catch (NotSupportedException)
                {
                    // Some providers don't expose tools — that's fine
                }
                catch (NotImplementedException)
                {
                    // Some providers don't expose tools — that's fine
                }
            }

            logger.LogDebug("Fallback AI chat with {ToolCount} tool(s) available", tools.Count);

            // Load conversation history from the session store
            var history = await sessionStore.LoadAsync(sessionKey);
            logger.LogDebug(
                "Loaded {Count} message(s) from session '{SessionKey}'",
                history.Count, sessionKey);

            // Compact the session if it exceeds the token limit
            history = await CompactSessionAsync(sessionKey, history, chatClient);

            // Build the message list: system prompt + history + current user message
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System,
                    BuildSystemPrompt(message))
            };

            // Replay conversation history
            foreach (var entry in history)
            {
                var role = entry.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, entry.Content));
            }

            // Add the current user message
            messages.Add(new ChatMessage(ChatRole.User, message.Text));

            ChatResponse response;
            if (tools.Count > 0)
            {
                // Wrap in FunctionInvokingChatClient to automatically handle tool call loops:
                // AI requests a tool call → function is executed → result sent back → AI responds
                var functionCallingClient = new FunctionInvokingChatClient(chatClient);
                var options = new ChatOptions { Tools = tools };
                response = await functionCallingClient.GetResponseAsync(messages, options);
            }
            else
            {
                response = await chatClient.GetResponseAsync(messages);
            }

            string responseText = response.Text ?? "I received an empty response from the AI provider.";
            LogUsageDetails(provider.Name, response);

            // Persist both user and assistant messages only after a successful AI response
            await sessionStore.AppendAsync(sessionKey,
                new SessionMessage("user", message.Text, message.Timestamp));
            await sessionStore.AppendAsync(sessionKey,
                new SessionMessage("assistant", responseText, DateTimeOffset.UtcNow));

            return responseText;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallback AI chat failed using provider '{Name}'", provider.Name);
            return "I had trouble getting a response from the AI. Please try again.";
        }
    }

    /// <summary>
    /// Logs token usage details from the AI response, including prompt cache metrics when available.
    /// Cache metrics (CacheCreationInputTokens, CacheReadInputTokens) are provided by Anthropic
    /// when prompt caching is enabled, allowing monitoring of cache effectiveness.
    /// </summary>
    private void LogUsageDetails(string providerName, ChatResponse response)
    {
        if (response.Usage is not { } usage)
            return;

        logger.LogDebug(
            "AI usage for '{Provider}': input={InputTokens}, output={OutputTokens}",
            providerName, usage.InputTokenCount, usage.OutputTokenCount);

        if (usage.AdditionalCounts is { Count: > 0 } counts)
        {
            if (counts.TryGetValue("CacheCreationInputTokens", out long cacheCreation) && cacheCreation > 0)
                logger.LogInformation(
                    "Prompt cache write for '{Provider}': {CacheCreationTokens} tokens written to cache",
                    providerName, cacheCreation);

            if (counts.TryGetValue("CacheReadInputTokens", out long cacheRead) && cacheRead > 0)
                logger.LogInformation(
                    "Prompt cache hit for '{Provider}': {CacheReadTokens} tokens read from cache",
                    providerName, cacheRead);
        }
    }

    private static string BuildSystemPrompt(IncomingMessage message)
    {
        var prompt =
            "You are Andúril, a personal AI assistant. " +
            "You have access to integration tools that let you interact with external services " +
            "like Gmail, GitHub, Sentry, Calendar, Slack, and Medium on behalf of the user. " +
            "When the user asks about their emails, calendar, code, errors, chat history, or linked articles, " +
            "use the available tools to fulfill their request. " +
            "Always be helpful and concise.\n\n" +
            "Formatting: Use clear structure in your responses. " +
            "Use **bold** for emphasis and section headings. " +
            "Use bullet points (- item) for lists. " +
            "Use `code` for inline code and ``` for code blocks. " +
            "Use ~strikethrough~ sparingly. " +
            "Use [text](url) for links. " +
            "Keep responses scannable with short paragraphs and clear section breaks.";

        if (!message.Platform.Equals("slack", StringComparison.OrdinalIgnoreCase))
            return prompt;

        var currentThreadId = message.ThreadId ?? message.Id;

        return prompt + "\n\n" +
               "Current Slack conversation context:\n" +
               $"- Channel ID: {message.ChannelId}\n" +
               $"- Current thread timestamp: {currentThreadId}\n" +
               $"- Direct message: {(message.IsDirectMessage ? "yes" : "no")}\n" +
               "If the user refers to 'this conversation', 'this thread', nearby Slack messages, or Slack-shared links, " +
               "prefer the Slack query tools with the current channel and thread before asking the user for IDs. " +
               "If you find Medium URLs in Slack history, you can fetch them with the Medium article tool.";
    }

    /// <summary>
    /// Estimates the total token count for a list of session messages.
    /// Uses the ~4 characters per token heuristic (matching OpenClaw).
    /// Each message is serialized to JSON for a consistent size estimate.
    /// </summary>
    internal static int EstimateTokens(IReadOnlyList<SessionMessage> messages)
    {
        int totalChars = 0;
        foreach (var msg in messages)
            totalChars += JsonSerializer.Serialize(msg).Length;

        return totalChars / 4;
    }

    /// <summary>
    /// Compacts the session if the estimated token count exceeds the configured threshold.
    /// Splits messages in half, summarizes the older half via AI, and replaces them
    /// with a single summary message. Preserves key facts about the user.
    /// </summary>
    private async Task<IReadOnlyList<SessionMessage>> CompactSessionAsync(
        string sessionKey,
        IReadOnlyList<SessionMessage> messages,
        IChatClient chatClient)
    {
        int maxTokens = sessionOptions.Value.MaxTokens;

        // Treat non-positive MaxTokens as "compaction disabled" to avoid unnecessary AI calls
        if (maxTokens <= 0)
            return messages;

        if (EstimateTokens(messages) < maxTokens)
            return messages;

        int split = messages.Count / 2;
        var oldMessages = messages.Take(split).ToList();
        var recentMessages = messages.Skip(split).ToList();

        logger.LogInformation(
            "Compacting session '{SessionKey}': {Total} messages, splitting at {Split}",
            sessionKey, messages.Count, split);

        try
        {
            var summaryPrompt = new List<ChatMessage>
            {
                new(ChatRole.User,
                    "Summarize this conversation concisely. Preserve:\n" +
                    "- Key facts about the user (name, preferences)\n" +
                    "- Important decisions made\n" +
                    "- Open tasks or TODOs\n\n" +
                    JsonSerializer.Serialize(oldMessages, new JsonSerializerOptions { WriteIndented = true }))
            };

            var summaryResponse = await chatClient.GetResponseAsync(summaryPrompt, new ChatOptions
            {
                MaxOutputTokens = 2000
            });

            string summaryText = summaryResponse.Text
                ?? "Previous conversation context (summary unavailable).";

            var compacted = new List<SessionMessage>(recentMessages.Count + 1)
            {
                new("system", $"[Previous conversation summary]\n{summaryText}", recentMessages[0].Timestamp)
            };
            compacted.AddRange(recentMessages);

            await sessionStore.ReplaceAllAsync(sessionKey, compacted);

            logger.LogInformation(
                "Session '{SessionKey}' compacted from {OldCount} to {NewCount} messages",
                sessionKey, messages.Count, compacted.Count);

            return compacted;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compact session '{SessionKey}' — using uncompacted history", sessionKey);
            return messages;
        }
    }


    public async ValueTask DisposeAsync()
    {
        // Dispose adapters
        foreach (var adapter in adapters)
        {
            if (adapter is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error disposing adapter '{Platform}'", adapter.Platform);
                }
            }
        }

        // Dispose integration tools
        foreach (var tool in integrationTools)
        {
            if (tool is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error disposing integration tool '{Name}'", tool.Name);
                }
            }
        }

        // Dispose providers
        foreach (var provider in aiProviders)
        {
            if (provider is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error disposing provider '{Name}'", provider.Name);
                }
            }
        }
    }
}
