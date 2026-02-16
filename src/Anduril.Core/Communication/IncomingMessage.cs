namespace Anduril.Core.Communication;

/// <summary>
/// A normalized incoming message from any communication platform (Slack, Teams, CLI, etc.).
/// </summary>
public class IncomingMessage
{
    /// <summary>
    /// Gets or sets the unique message ID from the originating platform.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the raw text content of the message.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the user ID of the sender.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets or sets the display name of the sender (if available).
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets or sets the channel or conversation ID.
    /// </summary>
    public required string ChannelId { get; init; }

    /// <summary>
    /// Gets or sets the name of the originating platform (e.g., "slack", "teams", "cli").
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Gets or sets the timestamp of the message.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the ID of the thread this message belongs to (if threaded).
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets or sets whether this message is a direct message to the bot.
    /// </summary>
    public bool IsDirectMessage { get; init; }

    /// <summary>
    /// Gets or sets platform-specific metadata that doesn't map to common fields.
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

