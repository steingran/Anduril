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

        string model = _options.Model ?? "claude-sonnet-4-5";

        // The Anthropic.SDK AnthropicClient.Messages implements IChatClient,
        // but requires the model to be specified per-request via ChatOptions.
        // We wrap it to automatically inject the model on every call.
        var client = new AnthropicClient(apiKey);
        var baseClient = client.Messages;
        _chatClient = new AnthropicChatClientWrapper(baseClient, model);

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

/// <summary>
/// Wrapper around Anthropic's IChatClient that automatically injects the model name
/// into ChatOptions for every request, since Anthropic requires model per-request.
/// </summary>
internal sealed class AnthropicChatClientWrapper(IChatClient innerClient, string model) : IChatClient
{
    public ChatClientMetadata Metadata => new("anthropic", providerUri: null, model);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mergedOptions = MergeOptions(options);
        return innerClient.GetResponseAsync(messages, mergedOptions, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mergedOptions = MergeOptions(options);
        return innerClient.GetStreamingResponseAsync(messages, mergedOptions, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(AnthropicChatClientWrapper)) return this;
        return innerClient.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (innerClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private ChatOptions MergeOptions(ChatOptions? options)
    {
        // Clone to avoid mutating the caller's instance
        var merged = options?.Clone() ?? new ChatOptions();
        // Only set model if not already specified by caller
        merged.ModelId ??= model;
        return merged;
    }
}

