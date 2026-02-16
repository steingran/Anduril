using Anduril.Core.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Anduril.AI.Providers;

/// <summary>
/// AI provider backed by OpenAI's API (GPT-4o, etc.) via Microsoft.Extensions.AI.
/// </summary>
public sealed class OpenAiProvider(IOptions<AiProviderOptions> options, ILogger<OpenAiProvider> logger)
    : IAiProvider
{
    private readonly AiProviderOptions _options = options.Value;
    private IChatClient? _chatClient;

    public string Name => "openai";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ApiKey) && _chatClient is not null;

    public bool SupportsChatCompletion => true;

    public IChatClient ChatClient =>
        _chatClient ?? throw new InvalidOperationException("OpenAI provider has not been initialized.");

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("OpenAI API key is not configured. OpenAI provider will remain unavailable.");
            return Task.CompletedTask;
        }

        var client = new OpenAIClient(apiKey);
        string model = _options.Model ?? "gpt-4o";

        _chatClient = client.GetChatClient(model).AsIChatClient();

        logger.LogInformation("OpenAI provider initialized with model {Model}", model);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        // OpenAI doesn't provide server-side tools — tools are registered by the caller.
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

