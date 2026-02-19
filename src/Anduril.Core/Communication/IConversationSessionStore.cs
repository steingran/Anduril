namespace Anduril.Core.Communication;

/// <summary>
/// Persists conversation history per session so the AI retains context across messages and restarts.
/// Sessions are keyed by a string identifier (e.g., "slack:U12345").
/// </summary>
public interface IConversationSessionStore
{
    /// <summary>
    /// Loads the conversation history for the given session key.
    /// Returns an empty list if no session exists yet.
    /// </summary>
    Task<IReadOnlyList<SessionMessage>> LoadAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a single message to the session file. This is append-only and crash-safe:
    /// if the process dies mid-write, at most one line is lost.
    /// </summary>
    Task AppendAsync(string sessionKey, SessionMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the entire session with the provided messages, rewriting the session file.
    /// Used after context compaction to persist the compacted conversation history.
    /// </summary>
    Task ReplaceAllAsync(string sessionKey, IReadOnlyList<SessionMessage> messages, CancellationToken cancellationToken = default);
}

