using Anduril.Core.AI;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Anthropic.SDK.Models;
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
    private AnthropicClient? _client;
    private IChatClient? _chatClient;
    private IReadOnlyList<ModelInfo>? _cachedModels;

    public string Name => "anthropic";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ApiKey) && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("Anthropic provider has not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Anthropic API key is not configured. Anthropic provider will remain unavailable.");
            return;
        }

        string model = _options.Model ?? "claude-sonnet-4-5";
        bool enablePromptCaching = _options.EnablePromptCaching;

        _client = new AnthropicClient(apiKey);

        // Fetch the model catalog eagerly so the UI can populate the model selector.
        _cachedModels = await FetchModelsAsync(cancellationToken);

        // The Anthropic.SDK AnthropicClient.Messages implements IChatClient,
        // but requires the model to be specified per-request via ChatOptions.
        // We wrap it to automatically inject the model on every call.
        _chatClient = new AnthropicChatClientWrapper(_client.Messages, model, enablePromptCaching);

        logger.LogInformation(
            "Anthropic provider initialized. Default model: {Model}, prompt caching: {PromptCaching}, {Count} model(s) available",
            model, enablePromptCaching, _cachedModels.Count);
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        // Anthropic doesn't provide server-side tools — tools are registered by the caller.
        return Task.FromResult<IReadOnlyList<AITool>>([]);
    }

    public async Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedModels is { Count: > 0 })
            return _cachedModels;

        _cachedModels = await FetchModelsAsync(cancellationToken);
        return _cachedModels;
    }

    public ValueTask DisposeAsync()
    {
        if (_chatClient is IDisposable disposable)
            disposable.Dispose();
        _chatClient = null;
        _client?.Dispose();
        _client = null;
        return ValueTask.CompletedTask;
    }

    private async Task<IReadOnlyList<ModelInfo>> FetchModelsAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
            return [];
        try
        {
            // Fetch up to 1000 models — well above Anthropic's current catalog size.
            var list = await _client.Models.ListModelsAsync(limit: 1000, ctx: cancellationToken);
            var models = list.Models
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .OrderBy(m => m.DisplayName ?? m.Id)
                .Select(m => new ModelInfo
                {
                    Id = m.Id,
                    DisplayName = !string.IsNullOrWhiteSpace(m.DisplayName) ? m.DisplayName : m.Id
                })
                .ToList();
            logger.LogDebug("Anthropic Models API returned {Count} model(s)", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Anthropic model list.");
            return [];
        }
    }
}

/// <summary>
/// Wrapper around Anthropic's IChatClient that automatically injects the model name
/// and prompt caching configuration into ChatOptions for every request,
/// since Anthropic requires model per-request.
/// </summary>
internal sealed class AnthropicChatClientWrapper(
    IChatClient innerClient,
    string model,
    bool enablePromptCaching = false) : IChatClient
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

        if (enablePromptCaching)
        {
            // Inject RawRepresentationFactory to configure Anthropic's prompt caching.
            // The SDK uses this factory to create the base MessageParameters, which lets us
            // set PromptCaching = AutomaticToolsAndSystem so the API automatically caches
            // system prompts and tool definitions — reducing latency and cost for repeated calls.
            var existingFactory = merged.RawRepresentationFactory;
            merged.RawRepresentationFactory = client =>
            {
                var parameters = existingFactory?.Invoke(client) as MessageParameters ?? new MessageParameters();
                parameters.PromptCaching = PromptCacheType.AutomaticToolsAndSystem;
                return parameters;
            };
        }

        return merged;
    }
}

