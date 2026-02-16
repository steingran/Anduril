using System.Collections.Immutable;
using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills;

/// <summary>
/// Routes incoming messages to the most appropriate skill by matching trigger phrases.
/// Falls back to null (general AI conversation) when no skill matches.
/// </summary>
public class SkillRouter(ILogger<SkillRouter> logger) : ISkillRouter
{
    private readonly List<ISkillRunner> _runners = [];
    private ImmutableList<SkillInfo> _skillIndex = ImmutableList<SkillInfo>.Empty;

    public void RegisterRunner(ISkillRunner runner)
    {
        _runners.Add(runner);
        logger.LogDebug("Registered skill runner: {Type}", runner.SkillType);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var allSkills = new List<SkillInfo>();
        foreach (var runner in _runners)
        {
            var skills = await runner.GetSkillsAsync(cancellationToken);
            allSkills.AddRange(skills);
        }

        // Atomic replacement for thread-safety
        _skillIndex = allSkills.ToImmutableList();
        logger.LogInformation("Skill index refreshed: {Count} skill(s) available", _skillIndex.Count);
    }

    public Task<SkillInfo?> RouteAsync(IncomingMessage message, CancellationToken cancellationToken = default)
    {
        string text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult<SkillInfo?>(null);

        // Score each skill by checking how many triggers match the message text
        SkillInfo? bestMatch = null;
        int bestScore = 0;

        foreach (var skill in _skillIndex)
        {
            int score = CalculateTriggerScore(text, skill.Triggers);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = skill;
            }
        }

        if (bestMatch is not null)
        {
            logger.LogDebug(
                "Routed message to skill '{Skill}' (score: {Score})",
                bestMatch.Name, bestScore);
        }
        else
        {
            logger.LogDebug("No skill matched — falling back to general conversation");
        }

        return Task.FromResult(bestMatch);
    }

    /// <summary>
    /// Calculates a match score for a message against a set of trigger phrases.
    /// Uses case-insensitive substring matching. Higher score = better match.
    /// </summary>
    private static int CalculateTriggerScore(string messageText, IReadOnlyList<string> triggers)
    {
        int score = 0;
        string lowerText = messageText.ToLowerInvariant();

        foreach (string trigger in triggers)
        {
            if (string.IsNullOrWhiteSpace(trigger)) continue;

            string lowerTrigger = trigger.ToLowerInvariant();
            if (lowerText.Contains(lowerTrigger))
            {
                // Longer trigger phrases get higher scores (more specific match)
                score += lowerTrigger.Length;
            }
        }

        return score;
    }
}

