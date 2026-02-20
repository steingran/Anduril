namespace Anduril.Communication;

/// <summary>
/// Configuration options for the Signal communication adapter.
/// Connects to a signal-cli REST API instance for sending and receiving messages.
/// </summary>
public class SignalAdapterOptions
{
    /// <summary>
    /// Gets or sets the phone number registered with Signal (e.g., "+1234567890").
    /// This is the bot's identity on the Signal network.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the base URL of the signal-cli REST API (e.g., "http://localhost:8080").
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the polling interval in seconds for checking new messages.
    /// Default is 2 seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 2;
}

