namespace Anduril.Communication;

/// <summary>
/// Configuration options for the Slack communication adapter.
/// </summary>
public class SlackAdapterOptions
{
    /// <summary>
    /// Gets or sets whether the Slack adapter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Slack Bot OAuth token (xoxb-...).
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Gets or sets the Slack App-Level token for Socket Mode (xapp-...).
    /// </summary>
    public string? AppToken { get; set; }
}

