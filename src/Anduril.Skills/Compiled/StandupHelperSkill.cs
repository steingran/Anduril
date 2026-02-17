using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills.Compiled;

/// <summary>
/// Compiled skill that generates a standup summary by querying GitHub activity
/// and Office 365 Calendar events since the last standup.
/// </summary>
public class StandupHelperSkill(
    IEnumerable<IIntegrationTool> integrationTools,
    ILogger<StandupHelperSkill> logger) : ISkill
{
    public string Name => "standup-helper";
    public string Description => "Generates a daily standup summary from GitHub activity and calendar events.";

    public IReadOnlyList<string> Triggers =>
    [
        "standup",
        "daily standup",
        "what did I do yesterday",
        "my standup update",
        "generate standup"
    ];

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var since = CalculateLastStandupTime();
        var today = DateTime.UtcNow.Date;

        logger.LogInformation(
            "Generating standup summary since {Since:yyyy-MM-dd HH:mm} UTC",
            since);

        var sinceArgs = new Dictionary<string, object?> { ["since"] = since };
        string prSummary = await InvokeToolFunctionAsync("github", "github_list_pull_requests_since", sinceArgs, cancellationToken);
        string issueSummary = await InvokeToolFunctionAsync("github", "github_list_issues_since", sinceArgs, cancellationToken);
        string pastMeetings = await InvokeToolFunctionAsync("office365-calendar", "calendar_events_since", sinceArgs, cancellationToken);
        string todayMeetings = await InvokeToolFunctionAsync("office365-calendar", "calendar_today", cancellationToken: cancellationToken);

        string standup = FormatStandup(today, prSummary, issueSummary, pastMeetings, todayMeetings);
        return SkillResult.Ok(standup);
    }

    /// <summary>
    /// Calculates the timestamp of the last standup. Standups occur Monday and Wednesday at 09:25 UTC.
    /// </summary>
    internal static DateTime CalculateLastStandupTime(DateTime? now = null)
    {
        var current = now ?? DateTime.UtcNow;
        var today = current.Date.AddHours(9).AddMinutes(25);

        // Walk backwards from today to find the previous standup day (Mon or Wed)
        var candidate = current >= today ? current.Date : current.Date.AddDays(-1);

        for (int i = 0; i < 7; i++)
        {
            var day = candidate.AddDays(-i);
            if (day == current.Date && current < today)
            {
                // Today hasn't had standup yet, skip
                continue;
            }

            if (day.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday)
            {
                // Don't return today's standup time as "last standup" if we're generating now
                if (day == current.Date)
                    continue;

                return day.AddHours(9).AddMinutes(25);
            }
        }

        // Fallback: 3 days ago
        return current.AddDays(-3).Date.AddHours(9).AddMinutes(25);
    }

    private async Task<string> InvokeToolFunctionAsync(
        string toolName,
        string functionName,
        IDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var tool = integrationTools.FirstOrDefault(t =>
            t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool is null || !tool.IsAvailable)
        {
            logger.LogWarning("Integration tool '{Tool}' is not available", toolName);
            return $"_{toolName} integration unavailable_";
        }

        var function = tool.GetFunctions()
            .FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

        if (function is null)
        {
            logger.LogWarning("Function '{Function}' not found on tool '{Tool}'", functionName, toolName);
            return $"_Function {functionName} not found_";
        }

        try
        {
            var aiArgs = arguments is not null ? new AIFunctionArguments(arguments) : null;
            var result = await function.InvokeAsync(aiArgs, cancellationToken);
            return result?.ToString() ?? "_No data returned_";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invoking {Tool}/{Function}", toolName, functionName);
            return $"_Error querying {toolName}. Please try again later._";
        }
    }

    private static string FormatStandup(
        DateTime today,
        string prSummary,
        string issueSummary,
        string pastMeetings,
        string todayMeetings)
    {
        return $"""
            ### 📋 Standup — {today:yyyy-MM-dd}

            **Since Last Standup**
            _Pull Requests:_
            {prSummary}

            _Issues:_
            {issueSummary}

            _Meetings Attended:_
            {pastMeetings}

            **Today**
            _Scheduled Meetings:_
            {todayMeetings}

            **Blockers**
            - _Review and update as needed_
            """;
    }
}

