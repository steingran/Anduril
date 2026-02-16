using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.AI;

namespace Anduril.Host;

/// <summary>
/// Background service that orchestrates the full message-processing pipeline:
/// initializes AI providers, wires up skill routing, starts communication
/// adapters, and handles incoming messages end-to-end.
/// </summary>
public sealed class MessageProcessingService(
    IEnumerable<IAiProvider> aiProviders,
    IEnumerable<ICommunicationAdapter> adapters,
    ISkillRouter router,
    PromptSkillRunner promptRunner,
    CompiledSkillRunner compiledRunner,
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

        // 2. Register skill runners with the router and build the skill index
        compiledRunner.LoadFromDirectory();
        router.RegisterRunner(promptRunner);
        router.RegisterRunner(compiledRunner);
        await router.RefreshAsync(cancellationToken);

        // 3. Start communication adapters and subscribe to incoming messages
        foreach (var adapter in adapters)
        {
            try
            {
                // Store the handler so we can unsubscribe later
                Func<IncomingMessage, Task> handler = msg => OnMessageReceivedAsync(adapter, msg);
                _eventHandlers[adapter] = handler;
                adapter.MessageReceived += handler;

                await adapter.StartAsync(cancellationToken);
                logger.LogInformation("Communication adapter '{Platform}' started", adapter.Platform);
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
        try
        {
            logger.LogDebug(
                "Received message from {User} on {Platform}/{Channel}: {Text}",
                message.UserId, message.Platform, message.ChannelId,
                message.Text.Length > 80 ? message.Text[..80] + "…" : message.Text);

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

            // Send the response back through the same adapter
            await sourceAdapter.SendMessageAsync(new OutgoingMessage
            {
                Text = responseText,
                ChannelId = message.ChannelId,
                ThreadId = message.ThreadId ?? message.Id // reply in thread
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId}", message.Id);

            try
            {
                await sourceAdapter.SendMessageAsync(new OutgoingMessage
                {
                    Text = "Sorry, something went wrong while processing your message.",
                    ChannelId = message.ChannelId,
                    ThreadId = message.ThreadId ?? message.Id
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

        try
        {
            var chatClient = provider.ChatClient;
            var response = await chatClient.GetResponseAsync(message.Text);
            return response.Text ?? "I received an empty response from the AI provider.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallback AI chat failed using provider '{Name}'", provider.Name);
            return "I had trouble getting a response from the AI. Please try again.";
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
