using Anduril.Core.AI;
using Anduril.Core.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills;

/// <summary>
/// Skill runner for natural-language / prompt-based skills loaded from markdown files.
/// Executes skills by constructing a system prompt from the skill's Instructions and Output Format,
/// then sending the user's message to an <see cref="IChatClient"/> for completion.
/// </summary>
public class PromptSkillRunner(
    PromptSkillLoader loader,
    IEnumerable<IAiProvider> aiProviders,
    ILogger<PromptSkillRunner> logger,
    string skillsDirectory = "skills")
    : ISkillRunner
{
    private Dictionary<string, PromptSkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public string SkillType => "prompt";

    public async Task<IReadOnlyList<SkillInfo>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        await RefreshSkillsAsync(cancellationToken);

        return _skills.Values.Select(s => new SkillInfo
        {
            Name = s.Name,
            Description = s.Description,
            SkillType = SkillType,
            Triggers = s.Triggers,
            SourcePath = s.SourcePath
        }).ToList();
    }

    public bool CanHandle(SkillInfo skill) => skill.SkillType == SkillType;

    public async Task<SkillResult> ExecuteAsync(
        string skillName, SkillContext context, CancellationToken cancellationToken = default)
    {
        if (!_skills.TryGetValue(skillName, out var skill))
        {
            return SkillResult.Fail($"Prompt skill '{skillName}' not found.");
        }

        try
        {
            var provider = aiProviders.FirstOrDefault(p => p.IsAvailable && p.SupportsChatCompletion);
            if (provider is null)
            {
                bool hasToolProviders = aiProviders.Any(p => p.IsAvailable && !p.SupportsChatCompletion);
                string detail = hasToolProviders
                    ? "Tool providers are running but cannot handle chat. Configure OpenAI, Anthropic, Ollama, or LLamaSharp."
                    : "No AI providers are available at all. Check configuration.";
                throw new InvalidOperationException($"No chat-capable AI provider is available. {detail}");
            }
            var chatClient = provider.ChatClient;

            string systemPrompt = BuildSystemPrompt(skill);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, context.Message.Text)
            };

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

            string responseText = response.Text ?? string.Empty;

            logger.LogDebug("Prompt skill '{Skill}' completed ({Length} chars)", skillName, responseText.Length);

            return SkillResult.Ok(responseText);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Prompt skill '{Skill}' failed", skillName);
            return SkillResult.Fail($"An error occurred while executing the skill.");
        }
    }

    /// <summary>
    /// Reloads all prompt skills from the skills directory.
    /// </summary>
    public async Task RefreshSkillsAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadFromDirectoryAsync(skillsDirectory, cancellationToken);
        _skills = loaded.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the full system prompt from a prompt skill's sections.
    /// </summary>
    private static string BuildSystemPrompt(PromptSkill skill)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(skill.Description))
            parts.Add($"You are: {skill.Description}");

        if (!string.IsNullOrWhiteSpace(skill.Instructions))
            parts.Add(skill.Instructions);

        if (skill.Tools.Count > 0)
            parts.Add($"Available tools: {string.Join(", ", skill.Tools)}");

        if (!string.IsNullOrWhiteSpace(skill.OutputFormat))
            parts.Add($"Output Format:\n{skill.OutputFormat}");

        return string.Join("\n\n", parts);
    }
}

