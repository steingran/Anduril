using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class SlackQueryToolTests
{
    [Test]
    public async Task InitializeAsync_WithMissingBotToken_LeavesUnavailable()
    {
        var client = new FakeSlackQueryClient();
        var tool = CreateTool(client, botToken: null);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(client.ValidateAuthenticationCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetFunctions_ReturnsExpectedSlackFunctions()
    {
        var tool = CreateTool(new FakeSlackQueryClient());
        var names = tool.GetFunctions().Select(function => function.Name).ToList();

        await Assert.That(names.Count).IsEqualTo(3);
        await Assert.That(names).Contains("slack_search_messages");
        await Assert.That(names).Contains("slack_get_recent_messages");
        await Assert.That(names).Contains("slack_get_thread_messages");
    }

    [Test]
    public async Task SearchMessagesAsync_ResolvesChannelNameAndFiltersByKeywordAndDate()
    {
        var client = new FakeSlackQueryClient
        {
            ChannelLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["general"] = "C123"
            },
            HistoryByChannel = new Dictionary<string, IReadOnlyList<SlackMessageSummary>>(StringComparer.OrdinalIgnoreCase)
            {
                ["C123"] =
                [
                    CreateMessage("C123", "m-1", "release shipped yesterday", new DateTimeOffset(2026, 3, 4, 10, 0, 0, TimeSpan.Zero)),
                    CreateMessage("C123", "m-2", "release shipped today", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero)),
                    CreateMessage("C123", "m-3", "totally unrelated", new DateTimeOffset(2026, 3, 5, 11, 0, 0, TimeSpan.Zero))
                ]
            }
        };

        var tool = CreateTool(client);
        await tool.InitializeAsync();

        var result = await tool.SearchMessagesAsync(
            channels: "general",
            keyword: "release",
            oldest: "2026-03-05T00:00:00Z",
            latest: "2026-03-06T00:00:00Z",
            limit: 10);

        await Assert.That(tool.IsAvailable).IsTrue();
        await Assert.That(client.ValidateAuthenticationCallCount).IsEqualTo(1);
        await Assert.That(result).Contains("#general");
        await Assert.That(result).Contains("release shipped today");
        await Assert.That(result.Contains("release shipped yesterday", StringComparison.Ordinal)).IsFalse();
        await Assert.That(result.Contains("totally unrelated", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task SearchMessagesAsync_SearchesAllRequestedChannelsBeforeApplyingLimit()
    {
        var client = new FakeSlackQueryClient
        {
            ChannelLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["general"] = "C123",
                ["random"] = "C456"
            },
            HistoryByChannel = new Dictionary<string, IReadOnlyList<SlackMessageSummary>>(StringComparer.OrdinalIgnoreCase)
            {
                ["C123"] =
                [
                    CreateMessage("C123", "m-1", "release shipped yesterday", new DateTimeOffset(2026, 3, 4, 10, 0, 0, TimeSpan.Zero))
                ],
                ["C456"] =
                [
                    CreateMessage("C456", "m-2", "release shipped today", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero))
                ]
            }
        };

        var tool = CreateTool(client);
        await tool.InitializeAsync();

        var result = await tool.SearchMessagesAsync("general,random", "release", limit: 1);

        await Assert.That(result).Contains("#random");
        await Assert.That(result).Contains("release shipped today");
        await Assert.That(result.Contains("release shipped yesterday", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task SearchMessagesAsync_LowLimit_RequestsOnlyNeededMessagesPerPage()
    {
        var client = new FakeSlackQueryClient
        {
            ChannelLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["general"] = "C123"
            },
            HistoryResponder = (channelId, _, _, limit, _) =>
            (
                (IReadOnlyList<SlackMessageSummary>)
                [
                    CreateMessage(channelId, "m-1", "release shipped", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero))
                ],
                false,
                null)
        };

        var tool = CreateTool(client, configure: options => options.SearchPageSize = 100);
        await tool.InitializeAsync();

        _ = await tool.SearchMessagesAsync("general", "release", limit: 5);

        await Assert.That(client.RequestedHistoryLimits.Count).IsEqualTo(1);
        await Assert.That(client.RequestedHistoryLimits[0]).IsEqualTo(5);
    }

    [Test]
    public async Task GetRecentMessagesAsync_ReturnsMessagesInChronologicalOrder()
    {
        var client = new FakeSlackQueryClient
        {
            HistoryByChannel = new Dictionary<string, IReadOnlyList<SlackMessageSummary>>(StringComparer.OrdinalIgnoreCase)
            {
                ["C555"] =
                [
                    CreateMessage("C555", "m-2", "second", new DateTimeOffset(2026, 3, 5, 11, 0, 0, TimeSpan.Zero)),
                    CreateMessage("C555", "m-1", "first", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero))
                ]
            }
        };

        var tool = CreateTool(client);
        await tool.InitializeAsync();

        var result = await tool.GetRecentMessagesAsync("C555", limit: 10);

        await Assert.That(result.IndexOf("first", StringComparison.Ordinal)).IsLessThan(result.IndexOf("second", StringComparison.Ordinal));
    }

    [Test]
    public async Task GetThreadMessagesAsync_ReturnsRepliesInChronologicalOrder()
    {
        var client = new FakeSlackQueryClient
        {
            ThreadRepliesByChannelAndThread = new Dictionary<string, IReadOnlyList<SlackMessageSummary>>(StringComparer.OrdinalIgnoreCase)
            {
                ["C777|thread-1"] =
                [
                    CreateMessage("C777", "m-2", "second reply", new DateTimeOffset(2026, 3, 5, 11, 0, 0, TimeSpan.Zero), threadTs: "thread-1"),
                    CreateMessage("C777", "m-1", "first reply", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), threadTs: "thread-1")
                ]
            }
        };

        var tool = CreateTool(client);
        await tool.InitializeAsync();

        var result = await tool.GetThreadMessagesAsync("C777", "thread-1", limit: 10);

        await Assert.That(result).Contains("Slack thread messages for C777 (thread-1)");
        await Assert.That(result.IndexOf("first reply", StringComparison.Ordinal)).IsLessThan(result.IndexOf("second reply", StringComparison.Ordinal));
    }

    [Test]
    public async Task GetThreadMessagesAsync_UsesRemainingLimitForLaterPages()
    {
        var client = new FakeSlackQueryClient
        {
            ReplyResponder = (channelId, threadTs, limit, cursor) => cursor switch
            {
                null =>
                (
                    (IReadOnlyList<SlackMessageSummary>)
                    [
                        CreateMessage(channelId, "m-1", "first reply", new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), threadTs),
                        CreateMessage(channelId, "m-2", "second reply", new DateTimeOffset(2026, 3, 5, 11, 0, 0, TimeSpan.Zero), threadTs)
                    ],
                    true,
                    "page-2"
                ),
                "page-2" =>
                (
                    (IReadOnlyList<SlackMessageSummary>)
                    [
                        CreateMessage(channelId, "m-3", "third reply", new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero), threadTs)
                    ],
                    false,
                    null
                ),
                _ => ((IReadOnlyList<SlackMessageSummary>)[], false, null)
            }
        };

        var tool = CreateTool(client, configure: options => options.SearchPageSize = 10);
        await tool.InitializeAsync();

        var result = await tool.GetThreadMessagesAsync("C777", "thread-1", limit: 3);

        await Assert.That(client.RequestedReplyLimits.Count).IsEqualTo(2);
        await Assert.That(client.RequestedReplyLimits[0]).IsEqualTo(3);
        await Assert.That(client.RequestedReplyLimits[1]).IsEqualTo(1);
        await Assert.That(result).Contains("third reply");
    }

    [Test]
    public async Task SearchMessagesAsync_UnresolvedChannel_UsesPublicArgumentNameInErrorMessage()
    {
        var tool = CreateTool(new FakeSlackQueryClient());
        await tool.InitializeAsync();

        await Assert.That(async () => await tool.SearchMessagesAsync("missing", "release"))
            .ThrowsException()
            .WithMessageMatching("*'channels' argument*");
    }

    private static SlackQueryTool CreateTool(
        FakeSlackQueryClient client,
        string? botToken = "xoxb-test",
        Action<SlackQueryToolOptions>? configure = null)
    {
        var options = new SlackQueryToolOptions
        {
            DefaultMessageLimit = 20,
            MaximumMessageLimit = 100,
            SearchPageSize = 100,
            MaximumSearchPages = 10
        };
        configure?.Invoke(options);

        return new SlackQueryTool(
            Options.Create(options),
            botToken,
            NullLogger<SlackQueryTool>.Instance,
            _ => client);
    }

    private static SlackMessageSummary CreateMessage(
        string channelId,
        string messageTs,
        string text,
        DateTimeOffset timestamp,
        string? threadTs = null) => new()
    {
        ChannelId = channelId,
        MessageTs = messageTs,
        ThreadTs = threadTs,
        Timestamp = timestamp,
        UserId = "U123",
        Text = text,
        ReplyCount = 0
    };

    private sealed class FakeSlackQueryClient : ISlackQueryClient
    {
        public int ValidateAuthenticationCallCount { get; private set; }
        public IReadOnlyDictionary<string, string> ChannelLookup { get; init; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, IReadOnlyList<SlackMessageSummary>> HistoryByChannel { get; init; } =
            new Dictionary<string, IReadOnlyList<SlackMessageSummary>>();
        public IReadOnlyDictionary<string, IReadOnlyList<SlackMessageSummary>> ThreadRepliesByChannelAndThread { get; init; } =
            new Dictionary<string, IReadOnlyList<SlackMessageSummary>>();
        public List<int> RequestedHistoryLimits { get; } = [];
        public List<int> RequestedReplyLimits { get; } = [];
        public Func<string, DateTimeOffset?, DateTimeOffset?, int, string?, (IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)>? HistoryResponder { get; init; }
        public Func<string, string, int, string?, (IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)>? ReplyResponder { get; init; }

        public Task ValidateAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            ValidateAuthenticationCallCount++;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationHistoryAsync(
            string channelId,
            DateTimeOffset? oldest,
            DateTimeOffset? latest,
            int limit,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            RequestedHistoryLimits.Add(limit);

            if (HistoryResponder is not null)
                return Task.FromResult(HistoryResponder(channelId, oldest, latest, limit, cursor));

            HistoryByChannel.TryGetValue(channelId, out var messages);
            messages ??= [];
            return Task.FromResult(((IReadOnlyList<SlackMessageSummary>)messages, false, (string?)null));
        }

        public Task<(IReadOnlyList<SlackMessageSummary> Messages, bool HasMore, string? NextCursor)> GetConversationRepliesAsync(
            string channelId,
            string threadTs,
            int limit,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            RequestedReplyLimits.Add(limit);

            if (ReplyResponder is not null)
                return Task.FromResult(ReplyResponder(channelId, threadTs, limit, cursor));

            ThreadRepliesByChannelAndThread.TryGetValue($"{channelId}|{threadTs}", out var messages);
            messages ??= [];
            return Task.FromResult(((IReadOnlyList<SlackMessageSummary>)messages, false, (string?)null));
        }

        public Task<IReadOnlyDictionary<string, string>> ListConversationNamesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ChannelLookup);
    }
}
