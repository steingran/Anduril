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
    /// Sends a message to the platform and returns a platform-specific message identifier
    /// that can be used with <see cref="UpdateMessageAsync"/> (e.g., Slack's message timestamp).
    /// Returns <c>null</c> if the platform does not support message identification.
    /// </summary>
    Task<string?> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing message on the platform.
    /// Platforms that do not support message updates should silently ignore the call.
    /// </summary>
    /// <param name="messageId">The platform-specific message identifier returned by <see cref="SendMessageAsync"/>.</param>
    /// <param name="message">The updated message content. <see cref="OutgoingMessage.ChannelId"/> and
    /// <see cref="OutgoingMessage.ThreadId"/> must match the original message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateMessageAsync(string messageId, OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        // Default: no-op for platforms that don't support message updates (CLI, etc.)
        return Task.CompletedTask;
    }
}

