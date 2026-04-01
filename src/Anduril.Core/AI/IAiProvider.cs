using Microsoft.Extensions.AI;

namespace Anduril.Core.AI;

/// <summary>
/// Represents an AI provider that wraps an <see cref="IChatClient"/> and
/// exposes available tools as <see cref="AIFunction"/> instances.
/// </summary>
public interface IAiProvider : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique name of this provider (e.g., "openai", "anthropic", "ollama").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the provider is currently available and configured.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports direct chat completions.
    /// Tool-only providers (e.g., MCP providers) return <c>false</c>.
    /// </summary>
    bool SupportsChatCompletion { get; }

    /// <summary>
    /// Gets the underlying <see cref="IChatClient"/> for direct chat completions.
    /// Throws <see cref="NotSupportedException"/> if <see cref="SupportsChatCompletion"/> is <c>false</c>.
    /// </summary>
    IChatClient ChatClient { get; }

    /// <summary>
    /// Initializes the provider, performing any startup work (e.g., connecting to an MCP server,
    /// loading a local model, verifying API keys).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of AI-callable tools (functions) this provider exposes.
    /// For MCP-backed providers, these come from the MCP server's tool list.
    /// </summary>
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the models available through this provider, each with a technical ID and a
    /// human-readable display name. An empty list means the provider exposes only the single
    /// model from its configuration. Providers like Copilot and Anthropic that can enumerate
    /// remote models override this.
    /// </summary>
    Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ModelInfo>>([]);

    /// <summary>
    /// Returns a <see cref="IChatClient"/> configured for the given model.
    /// The default ignores <paramref name="model"/> and returns <see cref="ChatClient"/>.
    /// Providers that support dynamic model selection override this.
    /// </summary>
    IChatClient GetChatClientForModel(string model) => ChatClient;
}

