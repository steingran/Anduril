using Anduril.Core.Communication;

namespace Anduril.Core.Skills;

/// <summary>
/// Routes incoming messages to the most appropriate skill based on
/// trigger matching, intent detection, or AI-based classification.
/// </summary>
public interface ISkillRouter
{
    /// <summary>
    /// Determines the best skill to handle the given message.
    /// Returns null if no skill matches and the message should be handled by general conversation.
    /// </summary>
    Task<SkillInfo?> RouteAsync(IncomingMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a skill runner that provides skills for routing.
    /// </summary>
    void RegisterRunner(ISkillRunner runner);

    /// <summary>
    /// Refreshes the router's internal index of available skills.
    /// Call this after skills are added, removed, or modified.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

