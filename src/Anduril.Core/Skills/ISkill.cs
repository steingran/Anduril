namespace Anduril.Core.Skills;

/// <summary>
/// Represents a strongly-typed, compiled skill that can be loaded as a plugin.
/// This is the developer-facing skill interface for compiled C# skills.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Gets the unique name of this skill.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of what this skill does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the trigger phrases or patterns that activate this skill.
    /// </summary>
    IReadOnlyList<string> Triggers { get; }

    /// <summary>
    /// Executes the skill with the given context and returns a result.
    /// </summary>
    Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default);
}

