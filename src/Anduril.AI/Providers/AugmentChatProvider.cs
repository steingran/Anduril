using Anduril.Core.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Providers;

/// <summary>
/// Chat-capable AI provider that communicates with the Augment Code HTTP API.
/// Unlike <see cref="AugmentMcpProvider"/> (tool-only via MCP), this provider
/// can handle direct conversation through Augment's <c>/chat-stream</c> endpoint.
/// </summary>
public sealed class AugmentChatProvider(IOptions<AiProviderOptions> options, ILogger<AugmentChatProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private AugmentChatClient? _chatClient;

    public string Name => "augment-chat";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ApiKey) && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException(
            "Augment Chat provider has not been initialized. Call InitializeAsync() first.");

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "Augment Chat provider skipped — no API key configured. " +
                "Set AI:AugmentChat:ApiKey in appsettings.json or user-secrets.");
            return Task.CompletedTask;
        }

        string endpoint = _options.Endpoint ?? "https://api.augmentcode.com";
        string model = _options.Model ?? "claude-sonnet-4-20250514";

        _chatClient = new AugmentChatClient(endpoint, apiKey, model, logger);

        logger.LogInformation(
            "Augment Chat provider initialized with model {Model} at {Endpoint}",
            model, endpoint);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        // Augment Chat provider doesn't expose server-side tools — tools come from MCP provider.
        return Task.FromResult<IReadOnlyList<AITool>>([]);
    }

    public ValueTask DisposeAsync()
    {
        _chatClient?.Dispose();
        _chatClient = null;
        return ValueTask.CompletedTask;
    }
}

