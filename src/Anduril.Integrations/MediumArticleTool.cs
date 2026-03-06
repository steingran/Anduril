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
    private readonly Func<Uri, CancellationToken, Task<string>> _fetchHtmlAsync;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<MediumArticleTool> _logger;
    private readonly MediumArticleToolOptions _options;
    private readonly TimeProvider _timeProvider;
    private bool _isInitialized;

    public MediumArticleTool(
        IOptions<MediumArticleToolOptions> options,
        ILogger<MediumArticleTool> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _fetchHtmlAsync = FetchHtmlAsync;
        _timeProvider = TimeProvider.System;
    }

    internal MediumArticleTool(
        IOptions<MediumArticleToolOptions> options,
        ILogger<MediumArticleTool> logger,
        Func<Uri, CancellationToken, Task<string>> fetchHtmlAsync,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _fetchHtmlAsync = fetchHtmlAsync;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => "medium-article";

    public string Description => "Medium article fetcher for retrieving article metadata and content from Medium-hosted pages and custom-domain Medium publications.";

    public bool IsAvailable => _isInitialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _isInitialized = true;

        if (string.IsNullOrWhiteSpace(_options.CookieHeader))
        {
            _logger.LogWarning(
                "Medium article integration is running without an authenticated Medium cookie header. Subscriber-only articles may return preview content only. Configure Integrations:MediumArticle:CookieHeader, preferably via an environment variable, to use your Medium subscription.");
            return;
        }

        _logger.LogInformation("Medium article integration initialized with an authenticated Medium cookie header.");
        await ValidateAuthenticatedCookieAsync(cancellationToken);
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

        string html;

        try
        {
            html = await _fetchHtmlAsync(uri, CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Medium article from {Url}", uri);
            return $"Failed to fetch article '{uri}'. The page may be unavailable or require authentication.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timed out fetching Medium article from {Url}", uri);
            return $"Timed out fetching article '{uri}'. The page may be slow or unavailable.";
        }

        if (!MediumUrlDetector.IsLikelyMediumArticle(uri, html))
            return $"The URL '{uri}' does not appear to be a Medium article or Medium-hosted publication page.";

        var article = MediumArticleParser.Parse(uri, html);
        var normalizedArticle = article with
        {
            MarkdownContent = Truncate(article.MarkdownContent, _options.MaximumContentLengthCharacters),
            PlainTextContent = Truncate(article.PlainTextContent, _options.MaximumContentLengthCharacters)
        };

        CacheArticle(uri.AbsoluteUri, normalizedArticle);
        CacheArticle(normalizedArticle.CanonicalUrl.AbsoluteUri, normalizedArticle);
        return FormatArticle(normalizedArticle);
    }

    private async Task<string> FetchHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        var httpClientFactory = _httpClientFactory ?? throw new InvalidOperationException("HTTP client factory is not available.");
        using var request = CreateRequestMessage(uri);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        using var response = await httpClientFactory.CreateClient(nameof(MediumArticleTool)).SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cts.Token);
    }

    internal HttpRequestMessage CreateRequestMessage(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            string.IsNullOrWhiteSpace(_options.UserAgent) ? "Anduril/0.1" : _options.UserAgent);

        if (!string.IsNullOrWhiteSpace(_options.CookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", _options.CookieHeader.Trim());

        return request;
    }

    private async Task ValidateAuthenticatedCookieAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ValidationUrl))
            return;

        if (!Uri.TryCreate(_options.ValidationUrl, UriKind.Absolute, out var validationUri))
        {
            _logger.LogWarning(
                "Medium article authenticated cookie validation skipped because Integrations:MediumArticle:ValidationUrl is not a valid absolute URL: {ValidationUrl}",
                _options.ValidationUrl);
            return;
        }

        try
        {
            var html = await _fetchHtmlAsync(validationUri, cancellationToken);

            if (!MediumUrlDetector.IsLikelyMediumArticle(validationUri, html))
            {
                _logger.LogWarning(
                    "Medium article authenticated cookie validation skipped because the configured validation URL '{ValidationUrl}' did not appear to be a Medium article. Choose a Medium article URL, ideally a subscriber-only article you can access.",
                    validationUri);
                return;
            }

            if (MediumUrlDetector.IsPaywalled(html))
            {
                _logger.LogWarning(
                    "Medium article authenticated cookie validation indicates the configured Medium cookie may be stale, expired, or insufficient. The validation URL '{ValidationUrl}' still appears paywalled.",
                    validationUri);
                return;
            }

            _logger.LogInformation("Medium article authenticated cookie validation succeeded for {ValidationUrl}", validationUri);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Medium article authenticated cookie validation timed out for {ValidationUrl}. Continuing without blocking startup.",
                validationUri);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Medium article authenticated cookie validation could not be completed for {ValidationUrl}. Continuing without blocking startup.",
                validationUri);
        }
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
