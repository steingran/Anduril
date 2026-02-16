namespace Anduril.Core.Communication;

/// <summary>
/// A normalized outgoing message to be sent to any communication platform.
/// </summary>
public class OutgoingMessage
{
    /// <summary>
    /// Gets or sets the text content to send.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the channel or conversation ID to send to.
    /// </summary>
    public required string ChannelId { get; init; }

    /// <summary>
    /// Gets or sets the thread ID to reply in (if threaded reply).
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets or sets optional structured blocks (e.g., Slack Block Kit, Adaptive Cards).
    /// The adapter is responsible for rendering these for its platform.
    /// </summary>
    public object? RichContent { get; init; }

    /// <summary>
    /// Gets or sets platform-specific metadata for the response.
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

