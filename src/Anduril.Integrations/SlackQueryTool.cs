using System.Text;
using Anduril.Communication;
using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackNet;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for querying Slack channel history and thread contents.
/// Reuses the configured Slack bot token and SlackNet Web API client.
/// </summary>
public sealed class SlackQueryTool : IIntegrationTool
{
    private readonly Func<string, ISlackQueryClient> _clientFactory;
    private readonly ILogger<SlackQueryTool> _logger;
    private readonly SlackQueryToolOptions _options;
    private readonly string? _botToken;
    private ISlackQueryClient? _client;

    public SlackQueryTool(
        IOptions<SlackQueryToolOptions> options,
        IOptions<SlackAdapterOptions> slackOptions,
        ILogger<SlackQueryTool> logger)
        : this(options, slackOptions.Value.BotToken, logger, CreateClient)
    {
    }

    internal SlackQueryTool(
        IOptions<SlackQueryToolOptions> options,
        string? botToken,
        ILogger<SlackQueryTool> logger,
        Func<string, ISlackQueryClient> clientFactory)
    {
        _options = options.Value;
        _botToken = botToken;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public string Name => "slack-query";

    public string Description => "Slack conversation lookup integration for channel history, keyword search, and thread retrieval.";

    public bool IsAvailable => _client is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
        {
            _logger.LogWarning("Slack BotToken not configured. Slack query integration will be unavailable.");
            return;
        }

        var client = _clientFactory(_botToken);

        try
        {
            await client.ValidateAuthenticationAsync(cancellationToken);
            _client = client;
            _logger.LogInformation("Slack query integration initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Slack query integration failed to validate Slack authentication.");
        }
    }

    public IReadOnlyList<AIFunction> GetFunctions() =>
    [
        AIFunctionFactory.Create(SearchMessagesAsync, "slack_search_messages",
            "Search Slack messages in one or more channels by keyword and optional date range. Pass channel IDs or names as a comma-separated list."),
        AIFunctionFactory.Create(GetRecentMessagesAsync, "slack_get_recent_messages",
            "Get recent Slack messages from a specific channel. Pass a channel ID or name."),
        AIFunctionFactory.Create(GetThreadMessagesAsync, "slack_get_thread_messages",
            "Get all available messages from a Slack thread using the channel ID/name and the thread timestamp."),
    ];

    public async Task<string> SearchMessagesAsync(
        string channels,
        string keyword,
        string oldest = "",
        string latest = "",
        int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("A keyword is required.", nameof(keyword));

        var client = GetClient();
        var requestedChannels = ParseChannelList(channels);
        var resolvedChannels = await ResolveChannelsAsync(client, requestedChannels, nameof(channels));
        var oldestTimestamp = ParseDateTimeOffset(oldest);
        var latestTimestamp = ParseDateTimeOffset(latest);
        var maxResults = ClampLimit(limit);
        int pageSize = Math.Max(1, Math.Min(_options.SearchPageSize, maxResults));
        var results = new List<SlackMessageSummary>();

        foreach (var channel in resolvedChannels)
        {
            string? cursor = null;

            for (var page = 0; page < _options.MaximumSearchPages; page++)
            {
                var response = await client.GetConversationHistoryAsync(
                    channel.ChannelId,
                    oldestTimestamp,
                    latestTimestamp,
                    pageSize,
                    cursor);

                var matches = response.Messages
                    .Where(message => MessageMatches(message, keyword, oldestTimestamp, latestTimestamp))
                    .Select(message => message with { ChannelName = channel.ChannelLabel })
                    .ToList();

                results.AddRange(matches);

                if (!response.HasMore || string.IsNullOrWhiteSpace(response.NextCursor))
                    break;

                cursor = response.NextCursor;
            }
        }

        var orderedResults = results
            .OrderByDescending(message => message.Timestamp)
            .Take(maxResults)
            .ToList();

        return FormatMessages(
            orderedResults,
            orderedResults.Count == 0
                ? $"No Slack messages found matching '{keyword}'."
                : $"Found {orderedResults.Count} Slack message(s) matching '{keyword}'.");
    }

    public async Task<string> GetRecentMessagesAsync(string channel, int limit = 20)
    {
        var client = GetClient();
        var resolvedChannel = (await ResolveChannelsAsync(client, [channel], nameof(channel))).Single();
        var response = await client.GetConversationHistoryAsync(
            resolvedChannel.ChannelId,
            oldest: null,
            latest: null,
            ClampLimit(limit),
            cursor: null);

        var messages = response.Messages
            .OrderBy(message => message.Timestamp)
            .Select(message => message with { ChannelName = resolvedChannel.ChannelLabel })
            .ToList();

        return FormatMessages(
            messages,
            messages.Count == 0
                ? $"No recent Slack messages found for {resolvedChannel.ChannelLabel}."
                : $"Recent Slack messages for {resolvedChannel.ChannelLabel}.");
    }

    public async Task<string> GetThreadMessagesAsync(string channel, string threadTs, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(threadTs))
            throw new ArgumentException("A Slack thread timestamp is required.", nameof(threadTs));

        var client = GetClient();
        var resolvedChannel = (await ResolveChannelsAsync(client, [channel], nameof(channel))).Single();
        var maxResults = ClampLimit(limit);
        var messages = new List<SlackMessageSummary>();
        string? cursor = null;

        for (var page = 0; page < _options.MaximumSearchPages; page++)
        {
            int remainingResults = maxResults - messages.Count;
            if (remainingResults <= 0)
                break;

            int pageSize = Math.Max(1, Math.Min(_options.SearchPageSize, remainingResults));
            var response = await client.GetConversationRepliesAsync(
                resolvedChannel.ChannelId,
                threadTs,
                pageSize,
                cursor);

            messages.AddRange(response.Messages.Select(message => message with { ChannelName = resolvedChannel.ChannelLabel }));

            if (!response.HasMore || string.IsNullOrWhiteSpace(response.NextCursor))
                break;

            cursor = response.NextCursor;
        }

        return FormatMessages(
            messages.OrderBy(message => message.Timestamp).Take(maxResults).ToList(),
            messages.Count == 0
                ? $"No thread messages found for {resolvedChannel.ChannelLabel} and thread {threadTs}."
                : $"Slack thread messages for {resolvedChannel.ChannelLabel} ({threadTs}).");
    }

    private static ISlackQueryClient CreateClient(string botToken)
    {
        var apiClient = new SlackServiceBuilder()
            .UseApiToken(botToken)
            .GetApiClient();

        return new SlackNetSlackQueryClient(apiClient);
    }

    private static List<string> ParseChannelList(string channels)
    {
        if (string.IsNullOrWhiteSpace(channels))
            throw new ArgumentException("At least one Slack channel ID or name is required.", nameof(channels));

        return channels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(channel => channel.Trim().TrimStart('#'))
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<(string ChannelId, string ChannelLabel)>> ResolveChannelsAsync(
        ISlackQueryClient client,
        IReadOnlyList<string> requestedChannels,
        string argumentName)
    {
        Dictionary<string, string>? channelLookup = null;
        var resolvedChannels = new List<(string ChannelId, string ChannelLabel)>(requestedChannels.Count);

        foreach (var requestedChannel in requestedChannels)
        {
            if (LooksLikeChannelId(requestedChannel))
            {
                resolvedChannels.Add((requestedChannel, requestedChannel));
                continue;
            }

            channelLookup ??= new Dictionary<string, string>(await client.ListConversationNamesAsync(), StringComparer.OrdinalIgnoreCase);

            if (!channelLookup.TryGetValue(requestedChannel, out var channelId))
                throw new ArgumentException($"Slack channel '{requestedChannel}' could not be resolved from the '{argumentName}' argument.", argumentName);

            resolvedChannels.Add((channelId, $"#{requestedChannel}"));
        }

        return resolvedChannels;
    }

    private int ClampLimit(int limit)
    {
        int requestedLimit = limit <= 0 ? _options.DefaultMessageLimit : limit;
        return Math.Clamp(requestedLimit, 1, Math.Max(1, _options.MaximumMessageLimit));
    }

    private static bool MessageMatches(
        SlackMessageSummary message,
        string keyword,
        DateTimeOffset? oldest,
        DateTimeOffset? latest)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
            return false;

        if (oldest is not null && message.Timestamp < oldest.Value)
            return false;

        if (latest is not null && message.Timestamp > latest.Value)
            return false;

        return message.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ParseDateTimeOffset(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
            throw new ArgumentException($"Could not parse '{value}' as a date/time.", nameof(value));

        return parsed;
    }

    private static bool LooksLikeChannelId(string channel) =>
        !string.IsNullOrWhiteSpace(channel) &&
        channel.Length >= 2 &&
        (channel[0] is 'C' or 'D' or 'G') &&
        channel.Skip(1).All(char.IsLetterOrDigit);

    private static string FormatMessages(IReadOnlyList<SlackMessageSummary> messages, string heading)
    {
        var builder = new StringBuilder();
        builder.AppendLine(heading);

        if (messages.Count == 0)
            return builder.ToString().TrimEnd();

        builder.AppendLine();

        foreach (var message in messages)
        {
            string channelLabel = message.ChannelName ?? message.ChannelId;
            string user = string.IsNullOrWhiteSpace(message.UserId) ? "unknown-user" : message.UserId;
            string subtype = string.IsNullOrWhiteSpace(message.Subtype) ? string.Empty : $" ({message.Subtype})";
            builder.Append("- [");
            builder.Append(message.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            builder.Append("] ");
            builder.Append(channelLabel);
            builder.Append(" | ");
            builder.Append(user);
            builder.Append(subtype);
            builder.Append(": ");
            builder.AppendLine(Truncate(message.Text, 400));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";

    private ISlackQueryClient GetClient() =>
        _client ?? throw new InvalidOperationException("Slack query integration is not initialized.");
}
