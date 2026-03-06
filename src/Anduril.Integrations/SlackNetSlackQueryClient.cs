using System.Globalization;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;

namespace Anduril.Integrations;

/// <summary>
/// SlackNet-backed implementation of <see cref="ISlackQueryClient"/>.
/// </summary>
internal sealed class SlackNetSlackQueryClient(ISlackApiClient apiClient) : ISlackQueryClient
{
    public async Task ValidateAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        _ = await apiClient.Auth.Test();
    }

    public async Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationHistoryAsync(
        string channelId,
        DateTimeOffset? oldest,
        DateTimeOffset? latest,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.Conversations.History(
            channelId,
            ToSlackTimestamp(latest),
            ToSlackTimestamp(oldest),
            inclusive: true,
            limit,
            includeAllMetadata: false,
            cursor,
            cancellationToken);

        return (
            response.Messages.Select(message => MapMessage(channelId, message)).ToList(),
            response.HasMore,
            response.ResponseMetadata?.NextCursor);
    }

    public async Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationRepliesAsync(
        string channelId,
        string threadTs,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.Conversations.Replies(
            channelId,
            threadTs,
            latestTs: null,
            oldestTs: null,
            inclusive: true,
            limit,
            includeAllMetadata: false,
            cursor,
            cancellationToken);

        return (
            response.Messages.Select(message => MapMessage(channelId, message)).ToList(),
            response.HasMore,
            response.ResponseMetadata?.NextCursor);
    }

    public async Task<IReadOnlyDictionary<string, string>> ListConversationNamesAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;

        do
        {
            var response = await apiClient.Conversations.List(
                excludeArchived: false,
                limit: 200,
                types:
                [
                    ConversationType.PublicChannel,
                    ConversationType.PrivateChannel,
                    ConversationType.Im,
                    ConversationType.Mpim
                ],
                cursor,
                teamId: null,
                cancellationToken);

            foreach (var channel in response.Channels)
            {
                results[channel.Id] = channel.Id;

                if (!string.IsNullOrWhiteSpace(channel.Name))
                    results[channel.Name] = channel.Id;

                if (!string.IsNullOrWhiteSpace(channel.NameNormalized))
                    results[channel.NameNormalized] = channel.Id;
            }

            cursor = response.ResponseMetadata?.NextCursor;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        return results;
    }

    private static SlackMessageSummary MapMessage(string channelId, MessageEvent message) => new()
    {
        ChannelId = channelId,
        MessageTs = message.Ts ?? string.Empty,
        ThreadTs = message.ThreadTs,
        Timestamp = message.Timestamp,
        UserId = message.User,
        Text = message.Text ?? string.Empty,
        ReplyCount = message.ReplyCount,
        Subtype = message.Subtype
    };

    private static string? ToSlackTimestamp(DateTimeOffset? timestamp) =>
        timestamp is null
            ? null
            : timestamp.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
