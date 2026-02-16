namespace Anduril.Core.Communication;

/// <summary>
/// Adapter for a communication platform (Slack, Teams, CLI, etc.).
/// Responsible for receiving messages, normalizing them, and sending responses.
/// </summary>
public interface ICommunicationAdapter : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of the platform this adapter connects to (e.g., "slack", "teams", "cli").
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Gets a value indicating whether this adapter is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised when a new message is received from the platform.
    /// </summary>
    event Func<IncomingMessage, Task> MessageReceived;

    /// <summary>
    /// Starts the adapter, connecting to the platform and beginning to listen for messages.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the adapter, disconnecting from the platform.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the platform.
    /// </summary>
    Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default);
}

