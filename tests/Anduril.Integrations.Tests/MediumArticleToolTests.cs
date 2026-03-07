using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class MediumArticleToolTests
{
    [Test]
    public async Task InitializeAsync_BecomesAvailable()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(SuccessfulFetch(uri, BasicMediumHtml)));

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenHttpOnly_DoesNotLogWarnings()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (uri, _) => Task.FromResult(SuccessfulFetch(uri, BasicMediumHtml)),
            retrievalMode: MediumArticleRetrievalMode.HttpOnly,
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InitializeAsync_WhenBrowserModeLacksProfilePath_LogsWarning()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (uri, _) => Task.FromResult(SuccessfulFetch(uri, BasicMediumHtml)),
            retrievalMode: MediumArticleRetrievalMode.Auto,
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Any(message =>
            message.Contains("BrowserRemoteDebuggingUrl", StringComparison.Ordinal) &&
            message.Contains("BrowserUserDataDirectory", StringComparison.Ordinal) &&
            message.Contains("browser fallback is disabled", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenBrowserModeUsesRemoteDebuggingUrl_DoesNotLogBrowserMisconfigurationWarning()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var tool = CreateTool(
            (uri, _) => Task.FromResult(SuccessfulFetch(uri, BasicMediumHtml)),
            retrievalMode: MediumArticleRetrievalMode.Auto,
            browserRemoteDebuggingUrl: "http://127.0.0.1:9222",
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(logger.WarningMessages.Any(message =>
            message.Contains("BrowserRemoteDebuggingUrl", StringComparison.Ordinal) ||
            message.Contains("BrowserUserDataDirectory", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task GetFunctions_ReturnsMediumGetArticle()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(SuccessfulFetch(uri, BasicMediumHtml)));
        var names = tool.GetFunctions().Select(function => function.Name).ToList();

        await Assert.That(names.Count).IsEqualTo(1);
        await Assert.That(names).Contains("medium_get_article");
    }

    [Test]
    public async Task GetArticleAsync_ParsesAndCachesArticleByCanonicalUrl()
    {
        var fetchCount = 0;
        var tool = CreateTool((_, _) =>
        {
            fetchCount++;
            var sourceUrl = new Uri("https://engineering.example.com/my-medium-post");
            return Task.FromResult(SuccessfulFetch(sourceUrl, CustomDomainMediumHtml));
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
        var tool = CreateTool((uri, _) => Task.FromResult(SuccessfulFetch(uri, "<html><body><p>Not Medium.</p></body></html>")));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://example.com/post");

        await Assert.That(result).Contains("does not appear to be a Medium article");
    }

    [Test]
    public async Task GetArticleAsync_GracefullyMarksPaywalledArticles()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(SuccessfulFetch(uri, PaywalledMediumHtml)));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://medium.com/@author/paywalled-post");

        await Assert.That(result).Contains("Paywalled: Yes");
        await Assert.That(result).Contains("Content preview unavailable because this Medium article appears to be paywalled");
    }

    [Test]
    public async Task GetArticleAsync_WhenFetchHitsCloudflareChallenge_ReturnsActionableMessage()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(MediumArticleFetchResult.Failed(
            uri,
            uri,
            MediumArticleRetrievalMethod.Http,
            MediumArticleFetchFailureReason.CloudflareChallenge,
            HttpStatusCode.Forbidden)));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://awstip.com/example-post");

        await Assert.That(result).Contains("behind a browser challenge");
        await Assert.That(result).Contains("custom domain");
    }

    [Test]
    public async Task GetArticleAsync_WhenFetchRequiresAuthentication_ReturnsBrowserSessionGuidance()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(MediumArticleFetchResult.Failed(
            uri,
            uri,
            MediumArticleRetrievalMethod.Http,
            MediumArticleFetchFailureReason.AuthenticationRequired,
            HttpStatusCode.Unauthorized)));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://medium.com/@author/member-post");

        await Assert.That(result).Contains("authenticated browser session");
        await Assert.That(result).Contains("browser-backed retrieval");
    }

    [Test]
    public async Task GetArticleAsync_WhenBrowserRetrieverIsUnavailable_ReturnsActionableMessage()
    {
        var tool = CreateTool((uri, _) => Task.FromResult(MediumArticleFetchResult.Failed(
            uri,
            uri,
            MediumArticleRetrievalMethod.Browser,
            MediumArticleFetchFailureReason.BrowserUnavailable,
            diagnosticMessage: "No browser profile configured.")));
        await tool.InitializeAsync();

        var result = await tool.GetArticleAsync("https://awstip.com/example-post");

        await Assert.That(result).Contains("Browser-backed retrieval is enabled but unavailable");
        await Assert.That(result).Contains("browser profile directory");
    }

    [Test]
    public async Task GetArticleAsync_WhenFetchFails_LogsFinalUrlAndDiagnostics()
    {
        var logger = new ListLogger<MediumArticleTool>();
        var requestedUrl = new Uri("https://medium.com/@author/post");
        var finalUrl = new Uri("https://search.brave.com/search?q=medium");
        var tool = CreateTool(
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                requestedUrl,
                finalUrl,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.CloudflareChallenge,
                HttpStatusCode.OK,
                diagnosticMessage: "Browser page diagnostics: FinalUrl='https://search.brave.com/search?q=medium'. PageTitle='Brave Search'. MatchesRequestedUrl=False.")),
            retrievalMode: MediumArticleRetrievalMode.BrowserOnly,
            browserRemoteDebuggingUrl: "http://127.0.0.1:9222",
            logger: logger);
        await tool.InitializeAsync();

        _ = await tool.GetArticleAsync(requestedUrl.AbsoluteUri);

        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
        await Assert.That(logger.WarningMessages[0]).Contains("FinalUrl: https://search.brave.com/search?q=medium");
        await Assert.That(logger.WarningMessages[0]).Contains("PageTitle='Brave Search'");
        await Assert.That(logger.WarningMessages[0]).Contains("MatchesRequestedUrl=False");
    }

    private static MediumArticleTool CreateTool(
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchAsync,
        MediumArticleRetrievalMode retrievalMode = MediumArticleRetrievalMode.HttpOnly,
        string? browserUserDataDirectory = null,
        string? browserRemoteDebuggingUrl = null,
        ILogger<MediumArticleTool>? logger = null) =>
        new(
            Options.Create(new MediumArticleToolOptions
            {
                RetrievalMode = retrievalMode,
                CacheDurationMinutes = 60,
                BrowserUserDataDirectory = browserUserDataDirectory,
                BrowserRemoteDebuggingUrl = browserRemoteDebuggingUrl,
                BrowserNavigationTimeoutSeconds = 60,
                RequestTimeoutSeconds = 20,
                UserAgent = string.Empty,
                MaximumContentLengthCharacters = 20_000
            }),
            logger ?? NullLogger<MediumArticleTool>.Instance,
            new DelegateMediumArticleRetriever(fetchAsync),
            TimeProvider.System);

    private static MediumArticleFetchResult SuccessfulFetch(Uri uri, string html) =>
        MediumArticleFetchResult.Successful(uri, uri, html, MediumArticleRetrievalMethod.Http);

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

    private sealed class DelegateMediumArticleRetriever(
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchAsync) : IMediumArticleRetriever
    {
        public Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken) =>
            fetchAsync(uri, cancellationToken);
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
