namespace Anduril.Host;

/// <summary>
/// Configuration options for the conversation session store.
/// </summary>
public class ConversationSessionOptions
{
    /// <summary>
    /// Gets or sets the directory where session JSONL files are stored.
    /// Defaults to "./sessions".
    /// </summary>
    public string SessionsDirectory { get; set; } = "./sessions";

    /// <summary>
    /// Gets or sets the maximum estimated token count before compaction is triggered.
    /// When exceeded, the older half of messages is summarized via AI and replaced
    /// with a compact summary. Defaults to 100,000 (~80% of a 128k context window).
    /// Token estimation uses ~4 characters per token.
    /// </summary>
    public int MaxTokens { get; set; } = 100_000;
}

