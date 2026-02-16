namespace Anduril.Core.Skills;

/// <summary>
/// Runs skills of a particular type (compiled, prompt-based, etc.).
/// Each runner knows how to load, parse, and execute skills in its format.
/// </summary>
public interface ISkillRunner
{
    /// <summary>
    /// Gets the type of skills this runner handles (e.g., "compiled", "prompt").
    /// </summary>
    string SkillType { get; }

    /// <summary>
    /// Returns all skills managed by this runner.
    /// </summary>
    Task<IReadOnlyList<SkillInfo>> GetSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether this runner can handle the given skill.
    /// </summary>
    bool CanHandle(SkillInfo skill);

    /// <summary>
    /// Executes a skill identified by name with the given context.
    /// </summary>
    Task<SkillResult> ExecuteAsync(string skillName, SkillContext context, CancellationToken cancellationToken = default);
}

