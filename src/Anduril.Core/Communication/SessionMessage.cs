namespace Anduril.Core.Communication;

/// <summary>
/// A single message entry in a conversation session, suitable for JSONL serialization.
/// </summary>
public sealed record SessionMessage(string Role, string Content, DateTimeOffset Timestamp);

