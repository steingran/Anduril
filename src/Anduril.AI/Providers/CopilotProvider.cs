using Anduril.Core.AI;
using AiModelInfo = Anduril.Core.AI.ModelInfo;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider backed by GitHub Copilot via the GitHub.Copilot.SDK.
/// Requires a GitHub token with the <c>copilot</c> scope (typically from
/// a Copilot Enterprise or Business subscription).
/// </summary>
public sealed class CopilotProvider(IOptions<AiProviderOptions> options, ILogger<CopilotProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private CopilotClient? _client;
    private IChatClient? _chatClient;
    private IReadOnlyList<AiModelInfo>? _cachedModels;

    public string Name => "copilot";

    public bool IsAvailable => _client is not null && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("Copilot provider has not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? token = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("GitHub token is not configured. Copilot provider will remain unavailable.");
            return;
        }

        try
        {
            _client = new CopilotClient(new CopilotClientOptions()); // { GithubToken = token });
            await _client.StartAsync(cancellationToken);

            _cachedModels = await FetchModelsFromSdkAsync(cancellationToken);
            logger.LogInformation("Copilot provider: {Count} model(s) available", _cachedModels.Count);

            string model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o" : _options.Model;
            _chatClient = new CopilotChatClientAdapter(_client, model, logger);

            logger.LogInformation("Copilot provider initialized with model {Model}", model);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize Copilot provider. It will remain unavailable.");
            await CleanupAsync();
        }
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AITool>>([]);

    public async Task<IReadOnlyList<AiModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        // Return the cache only if it has real results; an empty cache means the
        // init-time fetch failed and we should try again now that the client is up.
        if (_cachedModels is { Count: > 0 })
            return _cachedModels;

        _cachedModels = await FetchModelsFromSdkAsync(cancellationToken);
        return _cachedModels;
    }

    public IChatClient GetChatClientForModel(string model)
    {
        if (_client is null)
            throw new InvalidOperationException("Copilot provider has not been initialized.");
        return new CopilotChatClientAdapter(_client, model, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

    private async Task<IReadOnlyList<AiModelInfo>> FetchModelsFromSdkAsync(CancellationToken cancellationToken)
    {
        if (_client is null) return [];
        try
        {
            var sdkModels = await _client.ListModelsAsync(cancellationToken);
            var models = sdkModels
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .Select(m => new AiModelInfo { Id = m.Id, DisplayName = m.Id })
                .ToList();
            logger.LogDebug("SDK ListModelsAsync returned {Count} model(s)", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SDK ListModelsAsync failed; no models available.");
            return [];
        }
    }

    private async Task CleanupAsync()
    {
        _chatClient = null;

        if (_client is not null)
        {
            try
            {
                await _client.StopAsync();
            }
            catch
            {
                // Best-effort shutdown.
            }

            await _client.DisposeAsync();
            _client = null;
        }
    }

}
