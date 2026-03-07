namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Medium article fetcher integration.
/// </summary>
public sealed class MediumArticleToolOptions
{
    /// <summary>
    /// Gets or sets the retrieval mode used when loading Medium article pages.
    /// </summary>
    public MediumArticleRetrievalMode RetrievalMode { get; set; } = MediumArticleRetrievalMode.BrowserOnly;

    /// <summary>
    /// Gets or sets how long fetched articles remain cached in memory.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the persistent browser profile directory used for browser-backed retrieval.
    /// Use a dedicated profile rather than your daily-use browser profile when Anduril launches the browser itself.
    /// </summary>
    public string? BrowserUserDataDirectory { get; set; } = "./sessions/medium-browser";

    /// <summary>
    /// Gets or sets the optional remote debugging URL used to attach to an already-running Chrome or Edge session.
    /// Example: <c>http://127.0.0.1:9222</c>.
    /// </summary>
    public string? BrowserRemoteDebuggingUrl { get; set; } = "http://127.0.0.1:9222";

    /// <summary>
    /// Gets or sets the optional Playwright browser channel, such as <c>msedge</c> or <c>chrome</c>.
    /// </summary>
    public string? BrowserChannel { get; set; }

    /// <summary>
    /// Gets or sets the optional browser executable path used for browser-backed retrieval.
    /// </summary>
    public string? BrowserExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets whether browser-backed retrieval should run headless.
    /// Keep this disabled initially if you need to log in interactively.
    /// </summary>
    public bool BrowserHeadless { get; set; }

    /// <summary>
    /// Gets or sets the browser navigation timeout used when fetching article content.
    /// </summary>
    public int BrowserNavigationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets how long browser-backed retrieval should keep a visible browser window open
    /// when login or Cloudflare intervention is required before retrying.
    /// Set to 0 to disable manual-intervention waiting.
    /// </summary>
    public int BrowserManualInterventionWaitSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets how many times browser-backed retrieval should retry after waiting for
    /// manual login or Cloudflare challenge completion.
    /// </summary>
    public int BrowserManualInterventionRetryCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the HTTP request timeout used when fetching article content.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets the User-Agent header sent when fetching Medium article pages.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of content characters returned to the AI.
    /// </summary>
    public int MaximumContentLengthCharacters { get; set; } = 20000;
}
