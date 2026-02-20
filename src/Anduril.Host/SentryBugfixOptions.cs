namespace Anduril.Host;

/// <summary>
/// Configuration options for the automated Sentry bugfix service.
/// </summary>
public class SentryBugfixOptions
{
    /// <summary>
    /// Gets or sets whether the Sentry bugfix automation is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of occurrences before a bugfix is attempted.
    /// Issues below this threshold are ignored.
    /// </summary>
    public int OccurrenceThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the communication platform to send notifications to ("slack" or "teams").
    /// </summary>
    public string NotificationPlatform { get; set; } = "slack";

    /// <summary>
    /// Gets or sets the channel or conversation ID for bugfix notifications.
    /// </summary>
    public string? NotificationChannel { get; set; }

    /// <summary>
    /// Gets or sets the GitHub repository owner. Falls back to the GitHub integration's DefaultOwner if not set.
    /// </summary>
    public string? GitHubOwner { get; set; }

    /// <summary>
    /// Gets or sets the GitHub repository name. Falls back to the GitHub integration's DefaultRepo if not set.
    /// </summary>
    public string? GitHubRepo { get; set; }

    /// <summary>
    /// Gets or sets the path to the Augment Code CLI tool (auggie). Defaults to "auggie" (assumes it's on PATH).
    /// </summary>
    public string AugmentCliPath { get; set; } = "auggie";

    /// <summary>
    /// Gets or sets the branch name prefix for bugfix branches.
    /// </summary>
    public string BranchPrefix { get; set; } = "sentry-bugfix/";

    /// <summary>
    /// Gets or sets the timeout in minutes for the auggie CLI process. Defaults to 10 minutes.
    /// </summary>
    public int AuggieTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the base branch that pull requests target (e.g., "main", "master", "develop").
    /// Defaults to "main".
    /// </summary>
    public string BaseBranch { get; set; } = "main";

    /// <summary>
    /// Gets or sets the Sentry webhook secret for HMAC-SHA256 signature validation.
    /// Required when <see cref="Enabled"/> is <c>true</c> — the webhook endpoint will reject
    /// requests with HTTP 403 if the feature is enabled but this secret is not configured.
    /// </summary>
    public string? WebhookSecret { get; set; }
}

