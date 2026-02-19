namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Gmail integration.
/// Uses OAuth 2.0 with a stored refresh token for automatic token renewal.
/// </summary>
public class GmailToolOptions
{
    /// <summary>
    /// Gets or sets the OAuth 2.0 client ID from Google Cloud Console.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 client secret from Google Cloud Console.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 refresh token obtained through the consent flow.
    /// This is used to acquire access tokens automatically without user interaction.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the Gmail user ID to query. Defaults to "me" (the authenticated user).
    /// </summary>
    public string UserId { get; set; } = "me";

    /// <summary>
    /// Gets or sets the Google Cloud Pub/Sub topic for push notifications.
    /// Format: "projects/{project}/topics/{topic}".
    /// Required for real-time email notifications via Gmail watch.
    /// </summary>
    public string? PubSubTopic { get; set; }

    /// <summary>
    /// Gets or sets the local filesystem path where extracted attachments should be saved.
    /// </summary>
    public string? AttachmentSavePath { get; set; }

    /// <summary>
    /// Gets or sets the list of email processing rules.
    /// </summary>
    public List<GmailEmailRule> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of important sender addresses for priority filtering.
    /// </summary>
    public List<string> ImportantSenders { get; set; } = [];
}

