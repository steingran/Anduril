namespace Anduril.Integrations;

/// <summary>
/// Minimal Slack query client abstraction used by <see cref="SlackQueryTool"/>.
/// </summary>
internal interface ISlackQueryClient
{
    Task ValidateAuthenticationAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationHistoryAsync(
        string channelId,
        DateTimeOffset? oldest,
        DateTimeOffset? latest,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationRepliesAsync(
        string channelId,
        string threadTs,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> ListConversationNamesAsync(CancellationToken cancellationToken = default);
}