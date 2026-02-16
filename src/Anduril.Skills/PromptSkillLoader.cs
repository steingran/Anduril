using Anduril.Core.Skills;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills;

/// <summary>
/// Loads and parses markdown-based prompt skills from the file system.
/// Each .md file in the skills directory is parsed into a <see cref="PromptSkill"/>.
/// 
/// Expected markdown structure:
/// <code>
/// # Skill Name
/// Short description paragraph.
/// 
/// ## Trigger
/// - phrase one
/// - phrase two
/// 
/// ## Instructions
/// System prompt text...
/// 
/// ## Tools
/// - github
/// - sentry
/// 
/// ## Output Format
/// Description of expected output...
/// </code>
/// </summary>
public partial class PromptSkillLoader(ILogger<PromptSkillLoader> logger)
{
    /// <summary>
    /// Loads all markdown skill files from the given directory.
    /// </summary>
    public async Task<IReadOnlyList<PromptSkill>> LoadFromDirectoryAsync(
        string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            logger.LogWarning("Skills directory not found: {Path}", directoryPath);
            return [];
        }

        var skills = new List<PromptSkill>();
        string[] files = Directory.GetFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly);

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string content = await File.ReadAllTextAsync(file, cancellationToken);
                var skill = Parse(content, file);
                skills.Add(skill);
                logger.LogDebug("Loaded prompt skill '{Name}' from {Path}", skill.Name, file);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load skill from {Path}", file);
            }
        }

        logger.LogInformation("Loaded {Count} prompt skill(s) from {Path}", skills.Count, directoryPath);
        return skills;
    }

    /// <summary>
    /// Parses a single markdown string into a <see cref="PromptSkill"/>.
    /// </summary>
    public PromptSkill Parse(string markdown, string? sourcePath = null)
    {
        var sections = ParseSections(markdown);

        string name = sections.GetValueOrDefault("_title")
                      ?? (sourcePath is not null ? Path.GetFileNameWithoutExtension(sourcePath) : "unnamed");

        string description = sections.GetValueOrDefault("_description") ?? string.Empty;
        var triggers = ParseBulletList(sections.GetValueOrDefault("trigger"));
        string instructions = sections.GetValueOrDefault("instructions") ?? string.Empty;
        var tools = ParseBulletList(sections.GetValueOrDefault("tools"));
        string outputFormat = sections.GetValueOrDefault("output format") ?? string.Empty;

        return new PromptSkill
        {
            Name = name,
            Description = description,
            Triggers = triggers,
            Instructions = instructions,
            Tools = tools,
            OutputFormat = outputFormat,
            SourcePath = sourcePath
        };
    }

    /// <summary>
    /// Parses markdown into named sections keyed by heading text (lowercased).
    /// The title (H1) is stored under "_title", and text between H1 and first H2 under "_description".
    /// </summary>
    private static Dictionary<string, string> ParseSections(string markdown)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = markdown.Split('\n');

        string? currentSection = null;
        var sectionContent = new List<string>();

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            if (line.StartsWith("# ") && !line.StartsWith("## "))
            {
                // H1 heading — skill title
                FlushSection(sections, currentSection, sectionContent);
                sections["_title"] = line[2..].Trim();
                currentSection = "_description";
                sectionContent.Clear();
            }
            else if (line.StartsWith("## "))
            {
                // H2 heading — section name
                FlushSection(sections, currentSection, sectionContent);
                currentSection = line[3..].Trim().ToLowerInvariant();
                sectionContent.Clear();
            }
            else
            {
                sectionContent.Add(line);
            }
        }

        FlushSection(sections, currentSection, sectionContent);
        return sections;
    }

    private static void FlushSection(Dictionary<string, string> sections, string? key, List<string> lines)
    {
        if (key is null) return;
        string text = string.Join('\n', lines).Trim();
        if (!string.IsNullOrEmpty(text))
            sections[key] = text;
    }

    /// <summary>
    /// Parses a bullet list (lines starting with "- ") into a list of trimmed strings.
    /// </summary>
    private static List<string> ParseBulletList(string? section)
    {
        if (string.IsNullOrWhiteSpace(section)) return [];

        return section
            .Split('\n')
            .Select(l => l.TrimStart().TrimStart('-').Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
    }
}

