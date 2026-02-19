namespace Anduril.Core.AI;

/// <summary>
/// Configuration options for an AI provider.
/// </summary>
public class AiProviderOptions
{
    /// <summary>
    /// Gets or sets the provider type (e.g., "openai", "anthropic", "ollama", "augment", "llamasharp").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model identifier (e.g., "gpt-4o", "claude-sonnet-4-5", "qwen2.5:7b").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for cloud providers. Null for local providers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the base URL / endpoint override (e.g., "http://localhost:11434" for Ollama).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the path to a local model file (for LLamaSharp).
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the Augment CLI executable (for the MCP provider).
    /// </summary>
    public string? AugmentCliPath { get; set; }

    /// <summary>
    /// Gets or sets whether prompt caching is enabled for this provider.
    /// When enabled, the provider will use the API's caching features to reduce latency and cost
    /// for repeated system prompts and tool definitions. Currently supported by Anthropic.
    /// </summary>
    public bool EnablePromptCaching { get; set; }
}

