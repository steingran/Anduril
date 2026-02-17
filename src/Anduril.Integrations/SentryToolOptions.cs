namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Sentry integration.
/// </summary>
public class SentryToolOptions
{
    /// <summary>
    /// Gets or sets the Sentry authentication token (for the API).
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Gets or sets the Sentry organization slug.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Gets or sets the default Sentry project slug.
    /// </summary>
    public string? Project { get; set; }

    /// <summary>
    /// Gets or sets the Sentry API base URL. Defaults to https://sentry.io/api/0/.
    /// </summary>
    public string BaseUrl { get; set; } = "https://sentry.io/api/0/";
}

