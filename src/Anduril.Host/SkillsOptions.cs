namespace Anduril.Host;

/// <summary>
/// Configuration options for markdown prompt skill loading.
/// </summary>
public sealed class SkillsOptions
{
    public string SkillsDirectory { get; set; } = "skills";

    public string? LocalSkillsDirectory { get; set; } = "skills.local";
}