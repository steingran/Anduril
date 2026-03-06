namespace Anduril.Integrations;

/// <summary>
/// A normalized Slack message summary used by the Slack query tool.
/// </summary>
public sealed record SlackMessageSummary
{
    public required string ChannelId { get; init; }
    public string? ChannelName { get; init; }
    public required string MessageTs { get; init; }
    public string? ThreadTs { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? UserId { get; init; }
    public string Text { get; init; } = string.Empty;
    public int ReplyCount { get; init; }
    public string? Subtype { get; init; }
}