namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Medium article fetcher integration.
/// </summary>
public sealed class MediumArticleToolOptions
{
    /// <summary>
    /// Gets or sets how long fetched articles remain cached in memory.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the raw Cookie header value used for authenticated Medium requests.
    /// Leave unset to fetch articles anonymously.
    /// </summary>
    public string? CookieHeader { get; set; }

    /// <summary>
    /// Gets or sets an optional Medium article URL used during startup to validate
    /// whether the configured authenticated cookie still works.
    /// </summary>
    public string? ValidationUrl { get; set; }

    /// <summary>
    /// Gets or sets the HTTP request timeout used when fetching article content.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets the User-Agent header sent when fetching Medium article pages.
    /// </summary>
    public string UserAgent { get; set; } = "Anduril/0.1";

    /// <summary>
    /// Gets or sets the maximum number of content characters returned to the AI.
    /// </summary>
    public int MaximumContentLengthCharacters { get; set; } = 20000;
}
