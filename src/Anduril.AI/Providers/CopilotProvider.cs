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
    private bool _modelsLoaded;
    private readonly SemaphoreSlim _modelLock = new(1, 1);

    public string Name => "copilot";

    public bool IsAvailable => _client is not null && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("Copilot provider has not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clientOptions = new CopilotClientOptions();
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                clientOptions.GithubToken = _options.ApiKey;
            _client = new CopilotClient(clientOptions);
            await _client.StartAsync(cancellationToken);

            _cachedModels = await FetchModelsFromSdkAsync(cancellationToken);
            _modelsLoaded = true;
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
        if (_modelsLoaded && _cachedModels is not null)
            return _cachedModels;

        await _modelLock.WaitAsync(cancellationToken);
        try
        {
            if (_modelsLoaded && _cachedModels is not null)
                return _cachedModels;
            _cachedModels = await FetchModelsFromSdkAsync(cancellationToken);
            _modelsLoaded = true;
            return _cachedModels;
        }
        finally
        {
            _modelLock.Release();
        }
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
        _modelLock.Dispose();
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

            try
            {
                await _client.DisposeAsync();
            }
            catch
            {
                // Best-effort disposal — the client may not have started cleanly.
            }
            _client = null;
        }
    }

}
