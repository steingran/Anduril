using Anduril.Core.AI;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider using LLamaSharp for embedded local model inference (no Ollama dependency).
/// This is the fallback for environments where Ollama is not available.
/// </summary>
public sealed class LLamaSharpProvider(IOptions<AiProviderOptions> options, ILogger<LLamaSharpProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private IChatClient? _chatClient;

    public string Name => "llamasharp";

    public bool IsAvailable => _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("LLamaSharp provider has not been initialized.");

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? modelPath = _options.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            logger.LogWarning("LLamaSharp model path is not configured. LLamaSharp provider will remain unavailable.");
            return Task.CompletedTask;
        }

        if (!File.Exists(modelPath))
        {
            logger.LogWarning("LLamaSharp model file not found at: {Path}. LLamaSharp provider will remain unavailable.", modelPath);
            return Task.CompletedTask;
        }

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 20 // Offload some layers to GPU if available
        };

        _model = LLamaWeights.LoadFromFile(parameters);
        _context = _model.CreateContext(parameters);

        // LLamaSharp provides IChatClient via the AsChatClient() extension method
        var executor = new StatelessExecutor(_model, parameters);
        _chatClient = executor.AsChatClient();

        logger.LogInformation("LLamaSharp provider loaded model from {Path}", modelPath);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AITool>>([]);
    }

    public ValueTask DisposeAsync()
    {
        _chatClient = null;
        _context?.Dispose();
        _context = null;
        _model?.Dispose();
        _model = null;
        return ValueTask.CompletedTask;
    }
}

