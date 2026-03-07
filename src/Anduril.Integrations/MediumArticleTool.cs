using System.Collections.Concurrent;
using System.Text;
using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for fetching and parsing Medium articles, including custom-domain publications.
/// </summary>
public sealed class MediumArticleTool : IIntegrationTool
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, MediumArticleContent Article)> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IMediumArticleRetriever _retriever;
    private readonly ILogger<MediumArticleTool> _logger;
    private readonly MediumArticleToolOptions _options;
    private readonly TimeProvider _timeProvider;
    private bool _isInitialized;

    public MediumArticleTool(
        IOptions<MediumArticleToolOptions> options,
        ILogger<MediumArticleTool> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
        : this(options, logger, CreateRetriever(options, loggerFactory, httpClientFactory), TimeProvider.System)
    {
    }

    internal MediumArticleTool(
        IOptions<MediumArticleToolOptions> options,
        ILogger<MediumArticleTool> logger,
        IMediumArticleRetriever retriever,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _retriever = retriever;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => "medium-article";

    public string Description => "Medium article fetcher for retrieving article metadata and content from Medium-hosted pages and custom-domain Medium publications.";

    public bool IsAvailable => _isInitialized;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _isInitialized = true;
        WarnIfBrowserRetrievalIsMisconfigured();
        return Task.CompletedTask;
    }

    public IReadOnlyList<AIFunction> GetFunctions() =>
    [
        AIFunctionFactory.Create(GetArticleAsync, "medium_get_article",
            "Fetch a Medium article by URL, extract metadata and content, and return clean markdown."),
    ];

    public async Task<string> GetArticleAsync(string url)
    {
        EnsureInitialized();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("A valid absolute URL is required.", nameof(url));

        if (TryGetCachedArticle(uri.AbsoluteUri, out var cachedArticle))
            return FormatArticle(cachedArticle);

        var fetchResult = await _retriever.FetchAsync(uri, CancellationToken.None);

        if (!fetchResult.Success)
        {
            _logger.LogWarning(
                "Failed to fetch Medium article from {Url}. FinalUrl: {FinalUrl}. Method: {Method}. FailureReason: {FailureReason}. StatusCode: {StatusCode}. Diagnostics: {Diagnostics}",
                uri,
                fetchResult.FinalUrl,
                fetchResult.RetrievalMethod,
                fetchResult.FailureReason,
                fetchResult.StatusCode,
                fetchResult.DiagnosticMessage);
            return CreateFetchFailureMessage(uri, fetchResult.FailureReason);
        }

        if (!MediumUrlDetector.IsLikelyMediumArticle(fetchResult.FinalUrl, fetchResult.Html))
            return $"The URL '{uri}' does not appear to be a Medium article or Medium-hosted publication page.";

        var article = MediumArticleParser.Parse(fetchResult.FinalUrl, fetchResult.Html);
        var normalizedArticle = article with
        {
            MarkdownContent = Truncate(article.MarkdownContent, _options.MaximumContentLengthCharacters),
            PlainTextContent = Truncate(article.PlainTextContent, _options.MaximumContentLengthCharacters)
        };

        CacheArticle(uri.AbsoluteUri, normalizedArticle);
        CacheArticle(normalizedArticle.CanonicalUrl.AbsoluteUri, normalizedArticle);
        return FormatArticle(normalizedArticle);
    }

    private static string CreateFetchFailureMessage(Uri uri, MediumArticleFetchFailureReason failureReason) =>
        failureReason switch
        {
            MediumArticleFetchFailureReason.Timeout => $"Timed out fetching article '{uri}'. The page may be slow or unavailable.",
            MediumArticleFetchFailureReason.CloudflareChallenge => $"Failed to fetch article '{uri}'. The page appears to be behind a browser challenge on a Medium custom domain. Use browser-backed retrieval with either a dedicated browser profile or an attach-to-existing-Chrome session for pages like this.",
            MediumArticleFetchFailureReason.AuthenticationRequired => $"Failed to fetch article '{uri}'. The page may require Medium authentication or browser-backed retrieval with an authenticated browser session.",
            MediumArticleFetchFailureReason.Forbidden => $"Failed to fetch article '{uri}'. Access was forbidden. This custom-domain Medium page may require domain-specific authentication or a real browser session.",
            MediumArticleFetchFailureReason.BrowserUnavailable => $"Failed to fetch article '{uri}'. Browser-backed retrieval is enabled but unavailable. Configure either a remote debugging URL for an already-running Chrome or Edge session, or a dedicated browser profile directory plus a launchable browser channel or executable path.",
            MediumArticleFetchFailureReason.NetworkError => $"Failed to fetch article '{uri}'. The page may be unavailable or require authentication.",
            _ => $"Failed to fetch article '{uri}'. The page may be unavailable or require authentication.",
        };

    private static IMediumArticleRetriever CreateRetriever(
        IOptions<MediumArticleToolOptions> options,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory) =>
        new ConfigurableMediumArticleRetriever(
            options,
            new HttpMediumArticleRetriever(options, httpClientFactory),
            new PlaywrightMediumArticleRetriever(options, loggerFactory.CreateLogger<PlaywrightMediumArticleRetriever>()));

    private void WarnIfBrowserRetrievalIsMisconfigured()
    {
        if (_options.RetrievalMode == MediumArticleRetrievalMode.HttpOnly)
            return;

        if (MediumArticleBrowserConfiguration.IsConfigured(_options))
            return;

        var consequence = _options.RetrievalMode == MediumArticleRetrievalMode.BrowserOnly
            ? "Browser-only fetches will fail until either a remote debugging URL or a dedicated browser profile path is configured."
            : "HTTP retrieval will still work, but browser fallback is disabled until either a remote debugging URL or a dedicated browser profile path is configured.";

        _logger.LogWarning(
            "Medium article browser-backed retrieval is configured in {RetrievalMode} mode, but neither Integrations:MediumArticle:BrowserRemoteDebuggingUrl nor Integrations:MediumArticle:BrowserUserDataDirectory is configured. {Consequence}",
            _options.RetrievalMode,
            consequence);
    }

    private void CacheArticle(string cacheKey, MediumArticleContent article)
    {
        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(Math.Max(1, _options.CacheDurationMinutes));
        _cache[cacheKey] = (expiresAt, article);
    }

    private bool TryGetCachedArticle(string cacheKey, out MediumArticleContent article)
    {
        article = default!;

        if (!_cache.TryGetValue(cacheKey, out var entry))
            return false;

        if (entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            _cache.TryRemove(cacheKey, out _);
            return false;
        }

        article = entry.Article;
        return true;
    }

    private static string FormatArticle(MediumArticleContent article)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Title: {article.Title}");

        if (!string.IsNullOrWhiteSpace(article.Author))
            builder.AppendLine($"Author: {article.Author}");

        if (article.PublishedAt is not null)
            builder.AppendLine($"Published: {article.PublishedAt.Value:yyyy-MM-dd HH:mm:ss 'UTC'}");

        builder.AppendLine($"Source URL: {article.SourceUrl}");
        builder.AppendLine($"Canonical URL: {article.CanonicalUrl}");
        builder.AppendLine($"Paywalled: {(article.IsPaywalled ? "Yes" : "No")}");

        if (article.Tags.Count > 0)
            builder.AppendLine($"Tags: {string.Join(", ", article.Tags)}");

        builder.AppendLine();
        builder.AppendLine("Content:");
        builder.AppendLine(string.IsNullOrWhiteSpace(article.MarkdownContent)
            ? "_No article content could be extracted._"
            : article.MarkdownContent);
        return builder.ToString().TrimEnd();
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= Math.Max(1, maxLength)
            ? value
            : value[..Math.Max(1, maxLength)] + "\n\n_[content truncated]_";

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Medium article integration is not initialized.");
    }
}
