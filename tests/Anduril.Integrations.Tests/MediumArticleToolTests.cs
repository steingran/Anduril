using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class MediumArticleToolTests
{
    [Test]
    public async Task InitializeAsync_BecomesAvailable()
    {
        var tool = CreateTool((_, _) => Task.FromResult(BasicMediumHtml));

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WithoutCookieHeader_LogsWarning()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool((_, _) => Task.FromResult(BasicMediumHtml), logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
        await Assert.That(logger.WarningMessages[0]).Contains("without an authenticated Medium cookie header");
    }

    [Test]
    public async Task InitializeAsync_WithValidationUrlThatStillLooksPaywalled_LogsWarning()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (uri, _) => Task.FromResult(uri.AbsoluteUri.Contains("member-post", StringComparison.Ordinal)
                ? PaywalledMediumHtml
                : BasicMediumHtml),
            cookieHeader: "sid=abc; uid=123",
            validationUrl: "https://medium.com/@author/member-post",
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
        await Assert.That(logger.WarningMessages[0]).Contains("may be stale, expired, or insufficient");
    }

    [Test]
    public async Task InitializeAsync_WithHealthyValidationUrl_DoesNotLogWarning()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (uri, _) => Task.FromResult(uri.AbsoluteUri.Contains("member-post", StringComparison.Ordinal)
                ? BasicMediumHtml
                : CustomDomainMediumHtml),
            cookieHeader: "sid=abc; uid=123",
            validationUrl: "https://medium.com/@author/member-post",
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InitializeAsync_WhenValidationFetchFails_LogsWarningAndRemainsAvailable()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (_, _) => Task.FromException<string>(new HttpRequestException("boom")),
            cookieHeader: "sid=abc; uid=123",
            validationUrl: "https://medium.com/@author/member-post",
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsTrue();
        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
        await Assert.That(logger.WarningMessages[0]).Contains("could not be completed");
    }

    [Test]
    public async Task GetFunctions_ReturnsMediumGetArticle()
    {
        var tool = CreateTool((_, _) => Task.FromResult(BasicMediumHtml));
        var names = tool.GetFunctions().Select(function => function.Name).ToList();

        await Assert.That(names.Count).IsEqualTo(1);
        await Assert.That(names).Contains("medium_get_article");
    }

    [Test]
    public async Task CreateRequestMessage_WithCookieHeader_AddsCookieRequestHeader()
    {
        var tool = CreateTool((_, _) => Task.FromResult(BasicMediumHtml), cookieHeader: "sid=abc; uid=123");
        await tool.InitializeAsync();

        using var request = tool.CreateRequestMessage(new Uri("https://medium.com/@author/post"));
        var hasCookieHeader = request.Headers.TryGetValues("Cookie", out var values);

        await Assert.That(hasCookieHeader).IsTrue();
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Single()).IsEqualTo("sid=abc; uid=123");
    }

    [Test]
    public async Task GetArticleAsync_ParsesAndCachesArticleByCanonicalUrl()
    {
        var fetchCount = 0;
        var tool = CreateTool((_, _) =>
        {
            fetchCount++;
            return Task.FromResult(CustomDomainMediumHtml);
        });
        await tool.InitializeAsync();

        var firstResult = await tool.GetArticleAsync("https://engineering.example.com/my-medium-post");
        var secondResult = await tool.GetArticleAsync("https://medium.com/@author/my-medium-post");

        await Assert.That(fetchCount).IsEqualTo(1);
        await Assert.That(firstResult).Contains("Title: Medium Architecture Notes");
        await Assert.That(firstResult).Contains("Author: Ada Lovelace");
        await Assert.That(firstResult).Contains("Canonical URL: https://medium.com/@author/my-medium-post");
        await Assert.That(firstResult).Contains("Tags: architecture, slack");
        await Assert.That(firstResult).Contains("The first paragraph.");
        await Assert.That(secondResult).IsEqualTo(firstResult);
    }

    [Test]
    public async Task GetArticleAsync_ReturnsHelpfulMessageForNonMediumUrl()
    {
        var tool = CreateTool((_, _) => Task.FromResult("<html><body><p>Not Medium.</p></body></html>"));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://example.com/post");

        await Assert.That(result).Contains("does not appear to be a Medium article");
    }

    [Test]
    public async Task GetArticleAsync_GracefullyMarksPaywalledArticles()
    {
        var tool = CreateTool((_, _) => Task.FromResult(PaywalledMediumHtml));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://medium.com/@author/paywalled-post");

        await Assert.That(result).Contains("Paywalled: Yes");
        await Assert.That(result).Contains("Content preview unavailable because this Medium article appears to be paywalled");
    }

    private static MediumArticleTool CreateTool(
        Func<Uri, CancellationToken, Task<string>> fetchHtmlAsync,
        string? cookieHeader = null,
        string? validationUrl = null,
        ILogger<MediumArticleTool>? logger = null) =>
        new(
            Options.Create(new MediumArticleToolOptions
            {
                CacheDurationMinutes = 60,
                CookieHeader = cookieHeader,
                ValidationUrl = validationUrl,
                RequestTimeoutSeconds = 20,
                UserAgent = "Anduril/0.1",
                MaximumContentLengthCharacters = 20_000
            }),
            logger ?? NullLogger<MediumArticleTool>.Instance,
            fetchHtmlAsync,
            TimeProvider.System);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> WarningMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning)
                return;

            WarningMessages.Add(formatter(state, exception));
        }
    }

    private const string BasicMediumHtml = """
        <html>
        <head>
          <meta name="generator" content="Medium" />
          <title>Example Medium Post</title>
        </head>
        <body>
          <article>
            <p>Hello from Medium.</p>
          </article>
        </body>
        </html>
        """;

    private const string CustomDomainMediumHtml = """
        <html>
        <head>
          <meta name="generator" content="Medium" />
          <meta name="author" content="Ada Lovelace" />
          <meta property="article:published_time" content="2026-03-05T12:00:00Z" />
          <meta property="article:tag" content="architecture" />
          <meta property="article:tag" content="slack" />
          <link rel="canonical" href="https://medium.com/@author/my-medium-post" />
          <title>Medium Architecture Notes</title>
        </head>
        <body>
          <article>
            <h1>Medium Architecture Notes</h1>
            <p>The first paragraph.</p>
            <p>The second paragraph.</p>
          </article>
        </body>
        </html>
        """;

    private const string PaywalledMediumHtml = """
        <html>
        <head>
          <meta name="generator" content="Medium" />
          <title>Private Post</title>
        </head>
        <body>
          <div>Member-only story</div>
          <script type="application/ld+json">{"headline":"Private Post","author":{"name":"Ada Lovelace"}}</script>
        </body>
        </html>
        """;
}
