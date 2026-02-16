namespace Anduril.Skills;

/// <summary>
/// Represents a natural-language skill defined in a markdown file.
/// Contains the parsed sections: Trigger, Instructions, Tools, and Output Format.
/// </summary>
public class PromptSkill
{
    /// <summary>
    /// Gets or sets the skill name (derived from the file name or H1 heading).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the human-readable description (from the Description section or first paragraph).
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger phrases that activate this skill.
    /// Parsed from the "## Trigger" section of the markdown file.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; init; } = [];

    /// <summary>
    /// Gets or sets the system-prompt instructions for the AI.
    /// Parsed from the "## Instructions" section of the markdown file.
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the names of integration tools this skill may use.
    /// Parsed from the "## Tools" section of the markdown file.
    /// </summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>
    /// Gets or sets the expected output format description.
    /// Parsed from the "## Output Format" section of the markdown file.
    /// </summary>
    public string OutputFormat { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the full file path this skill was loaded from.
    /// </summary>
    public string? SourcePath { get; init; }
}

