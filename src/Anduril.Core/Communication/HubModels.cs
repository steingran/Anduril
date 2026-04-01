namespace Anduril.Core.Communication;

/// <summary>
/// Describes an available AI provider and its current state.
/// Used by the desktop app to populate the model selector.
/// </summary>
public sealed class ProviderInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Model { get; init; }

    /// <summary>
    /// Human-readable display name for the model (e.g., "Claude 3 Haiku").
    /// When null or empty the client should fall back to composing a name from
    /// <see cref="Name"/> and <see cref="Model"/>.
    /// </summary>
    public string? DisplayName { get; init; }

    public required bool IsAvailable { get; init; }
    public required bool SupportsChatCompletion { get; init; }
}

/// <summary>
/// A single streaming token from the AI response, sent from the hub to the client.
/// </summary>
public sealed class ChatStreamToken
{
    public required string ConversationId { get; init; }
    public required string Token { get; init; }
    public required bool IsComplete { get; init; }
    public string? Error { get; init; }

    /// <summary>
    /// True when <see cref="IsComplete"/> was triggered by the user pressing Stop
    /// rather than the model finishing naturally. The client uses this to render
    /// a "Stopped" indicator in the conversation.
    /// </summary>
    public bool WasCancelled { get; init; }
}

/// <summary>
/// Describes a conversation session.
/// </summary>
public sealed class ConversationInfo
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
