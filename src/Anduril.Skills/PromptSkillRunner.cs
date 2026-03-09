using Anduril.Core.AI;
using Anduril.Core.Integrations;
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
    IEnumerable<IIntegrationTool> integrationTools,
    ILogger<PromptSkillRunner> logger,
    string skillsDirectory = "skills",
    string? localSkillsDirectory = "skills.local")
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
        => await ExecuteCoreAsync(skillName, context, allowToolInvocation: true, cancellationToken);

    public async Task<SkillResult> ExecuteWithoutToolsAsync(
        string skillName, SkillContext context, CancellationToken cancellationToken = default)
        => await ExecuteCoreAsync(skillName, context, allowToolInvocation: false, cancellationToken);

    private async Task<SkillResult> ExecuteCoreAsync(
        string skillName,
        SkillContext context,
        bool allowToolInvocation,
        CancellationToken cancellationToken)
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
            var tools = allowToolInvocation
                ? await ResolveToolsAsync(skill, cancellationToken)
                : [];
            var resolvedToolNames = tools.Select(tool => tool.Name).ToList();
            string systemPrompt = BuildSystemPrompt(skill, context, resolvedToolNames);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, context.Message.Text)
            };
            ChatResponse response;

            if (tools.Count > 0)
            {
                var functionCallingClient = new FunctionInvokingChatClient(chatClient);
                response = await functionCallingClient.GetResponseAsync(
                    messages,
                    new ChatOptions { Tools = tools },
                    cancellationToken);
            }
            else
            {
                response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            }

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
        var loaded = new List<PromptSkill>();
        loaded.AddRange(await loader.LoadFromDirectoryAsync(skillsDirectory, cancellationToken));

        if (!string.IsNullOrWhiteSpace(localSkillsDirectory) && Directory.Exists(localSkillsDirectory))
            loaded.AddRange(await loader.LoadFromDirectoryAsync(localSkillsDirectory, cancellationToken));

        _skills = loaded
            .GroupBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the full system prompt from a prompt skill's sections.
    /// </summary>
    private async Task<IList<AITool>> ResolveToolsAsync(
        PromptSkill skill,
        CancellationToken cancellationToken)
    {
        if (skill.Tools.Count == 0)
            return [];

        var requestedTools = new HashSet<string>(skill.Tools, StringComparer.OrdinalIgnoreCase);
        var resolvedTools = new Dictionary<string, AITool>(StringComparer.OrdinalIgnoreCase);

        foreach (var integrationTool in integrationTools.Where(tool => tool.IsAvailable))
        {
            bool includeAllFunctions = requestedTools.Contains(integrationTool.Name);

            foreach (var function in integrationTool.GetFunctions())
            {
                if (!includeAllFunctions && !requestedTools.Contains(function.Name))
                    continue;

                resolvedTools[function.Name] = function;
            }
        }

        foreach (var provider in aiProviders.Where(provider => provider.IsAvailable))
        {
            try
            {
                bool includeAllFunctions = requestedTools.Contains(provider.Name);
                var providerTools = await provider.GetToolsAsync(cancellationToken);

                foreach (var providerTool in providerTools)
                {
                    if (!includeAllFunctions && !requestedTools.Contains(providerTool.Name))
                        continue;

                    resolvedTools[providerTool.Name] = providerTool;
                }
            }
            catch (NotSupportedException)
            {
                logger.LogDebug(
                    "Skipping tool discovery for AI provider '{ProviderName}' because provider tools are not supported.",
                    provider.Name);
            }
            catch (NotImplementedException)
            {
                logger.LogDebug(
                    "Skipping tool discovery for AI provider '{ProviderName}' because provider tools are not implemented.",
                    provider.Name);
            }
        }

        return [.. resolvedTools.Values.OrderBy(tool => tool.Name, StringComparer.Ordinal)];
    }

    private static string BuildSystemPrompt(
        PromptSkill skill,
        SkillContext context,
        IReadOnlyList<string> availableToolNames)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(skill.Description))
            parts.Add($"You are: {skill.Description}");

        var userId = context.UserId ?? context.Message.UserId;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            parts.Add(
                $"Current request context:\n" +
                $"- user_id: {userId}\n" +
                $"- platform: {context.Message.Platform}\n" +
                $"- channel_id: {context.ChannelId ?? context.Message.ChannelId}\n\n" +
                "When a tool requires a user ID, use the current user_id unless the user explicitly provides a different one.");
        }

        if (!string.IsNullOrWhiteSpace(skill.Instructions))
            parts.Add(skill.Instructions);

        if (availableToolNames.Count > 0)
            parts.Add($"Available tools: {string.Join(", ", availableToolNames)}");

        if (!string.IsNullOrWhiteSpace(skill.OutputFormat))
            parts.Add($"Output Format:\n{skill.OutputFormat}");

        return string.Join("\n\n", parts);
    }
}

