using Anduril.Core.AI;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider backed by Anthropic's API (Claude Sonnet, Opus, etc.).
/// Uses the Anthropic.SDK which implements <see cref="IChatClient"/>.
/// </summary>
public sealed class AnthropicProvider(IOptions<AiProviderOptions> options, ILogger<AnthropicProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private IChatClient? _chatClient;

    public string Name => "anthropic";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ApiKey) && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("Anthropic provider has not been initialized.");

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Anthropic API key is not configured. Anthropic provider will remain unavailable.");
            return Task.CompletedTask;
        }

        // The Anthropic.SDK AnthropicClient.Messages already implements IChatClient
        // Note: The model is specified per-request via ChatOptions, not at client initialization
        var client = new AnthropicClient(apiKey);
        _chatClient = client.Messages;

        string model = _options.Model ?? "claude-sonnet-4-20250514";
        logger.LogInformation("Anthropic provider initialized. Default model: {Model}", model);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        // Anthropic doesn't provide server-side tools — tools are registered by the caller.
        return Task.FromResult<IReadOnlyList<AITool>>([]);
    }

    public ValueTask DisposeAsync()
    {
        if (_chatClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _chatClient = null;
        return ValueTask.CompletedTask;
    }
}

