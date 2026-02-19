using System.Collections.Immutable;
using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills;

/// <summary>
/// Routes incoming messages to the most appropriate skill by matching trigger phrases.
/// Uses a minimum match ratio to ensure only confident matches are routed — messages
/// with weak/vague trigger overlap fall through to AI-based conversation with tools.
/// </summary>
public class SkillRouter(ILogger<SkillRouter> logger) : ISkillRouter
{
    /// <summary>
    /// Minimum ratio of matched trigger length to message length required to route
    /// to a skill. Below this threshold the message falls through to the AI with tools.
    /// A value of 0.3 means triggers must cover at least 30% of the message text.
    /// </summary>
    internal const double MinimumMatchRatio = 0.3;
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

        // Hybrid routing: only route if the match covers a meaningful portion of the message.
        // This ensures specific commands like "check my email" route to skills, while
        // conversational requests like "summarize my gmail emails for the last 24 hours"
        // fall through to the AI which can invoke the same tools more naturally.
        if (bestMatch is not null)
        {
            double ratio = (double)bestScore / text.Length;
            if (ratio < MinimumMatchRatio)
            {
                logger.LogDebug(
                    "Skill '{Skill}' matched with low confidence (score: {Score}, ratio: {Ratio:P0}) — falling through to AI",
                    bestMatch.Name, bestScore, ratio);
                bestMatch = null;
            }
            else
            {
                logger.LogDebug(
                    "Routed message to skill '{Skill}' (score: {Score}, ratio: {Ratio:P0})",
                    bestMatch.Name, bestScore, ratio);
            }
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

