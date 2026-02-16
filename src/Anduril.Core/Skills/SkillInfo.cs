namespace Anduril.Core.Skills;

/// <summary>
/// Metadata about a registered skill, used for routing and discovery.
/// </summary>
public class SkillInfo
{
    /// <summary>
    /// Gets or sets the unique name of the skill.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets a human-readable description of the skill.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the type of skill (e.g., "compiled", "prompt").
    /// </summary>
    public required string SkillType { get; init; }

    /// <summary>
    /// Gets or sets the trigger phrases or patterns that activate this skill.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; init; } = [];

    /// <summary>
    /// Gets or sets the source path (file path for prompt skills, assembly path for compiled skills).
    /// </summary>
    public string? SourcePath { get; init; }
}

