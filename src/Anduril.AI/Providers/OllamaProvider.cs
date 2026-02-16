using Anduril.Core.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider backed by Ollama for running local models (Qwen, Llama, etc.).
/// OllamaSharp's <see cref="OllamaApiClient"/> implements <see cref="IChatClient"/>.
/// </summary>
public sealed class OllamaProvider(IOptions<AiProviderOptions> options, ILogger<OllamaProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private OllamaApiClient? _client;

    public string Name => "ollama";

    public bool IsAvailable => _client is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _client ?? throw new InvalidOperationException("Ollama provider has not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string endpoint = _options.Endpoint ?? "http://localhost:11434";
        string? configuredModel = _options.Model;
        string model = configuredModel ?? "qwen2.5:7b";

        var uri = new Uri(endpoint);
        _client = new OllamaApiClient(uri, model);

        // Verify connectivity by listing local models
        try
        {
            var models = await _client.ListLocalModelsAsync(cancellationToken);
            var available = models.Select(m => m.Name).ToList();
            logger.LogInformation(
                "Ollama provider connected to {Endpoint}. Available models: {Models}. Using: {Model}",
                endpoint, string.Join(", ", available), model);

            if (!available.Any(m => m.StartsWith(model.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
            {
                if (configuredModel is not null)
                {
                    logger.LogWarning(
                        "Configured model '{Model}' not found locally. You may need to run 'ollama pull {Model}' first.", model, model);
                }
                else
                {
                    logger.LogWarning(
                        "Default model '{Model}' not found locally. Configure a different model in appsettings.json or run 'ollama pull {Model}'.", model, model);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not connect to Ollama at {Endpoint}. Is Ollama running?", endpoint);
            _client = null;
        }
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        // Ollama doesn't provide server-side tools — tools are registered by the caller.
        return Task.FromResult<IReadOnlyList<AITool>>([]);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _client = null;
        return ValueTask.CompletedTask;
    }
}

