namespace Anduril.Communication;

/// <summary>
/// Configuration options for the Teams communication adapter.
/// </summary>
public class TeamsAdapterOptions
{
    /// <summary>
    /// Gets or sets whether the Teams adapter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Microsoft App ID for the bot registration.
    /// </summary>
    public string? MicrosoftAppId { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft App Password (client secret).
    /// </summary>
    public string? MicrosoftAppPassword { get; set; }
}

