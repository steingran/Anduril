using Anduril.Core.Communication;

namespace Anduril.Core.Skills;

/// <summary>
/// Provides context for skill execution, including the incoming message,
/// conversation history, and access to integration tools.
/// </summary>
public class SkillContext
{
    /// <summary>
    /// Gets or sets the incoming message that triggered the skill.
    /// </summary>
    public required IncomingMessage Message { get; init; }

    /// <summary>
    /// Gets or sets the conversation history (most recent messages).
    /// </summary>
    public IReadOnlyList<IncomingMessage> ConversationHistory { get; init; } = [];

    /// <summary>
    /// Gets or sets a key-value bag for passing additional data between skills and the runtime.
    /// </summary>
    public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the ID of the user who sent the message.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets the channel or conversation ID where the message originated.
    /// </summary>
    public string? ChannelId { get; init; }
}

