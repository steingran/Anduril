using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills.Compiled;

/// <summary>
/// Compiled skill that provides Gmail email operations: summarize inbox,
/// overnight briefing, priority filtering, thread summaries, and rule-based processing.
/// Delegates to the Gmail integration tool for all API operations.
/// </summary>
public class GmailSkill(
    IEnumerable<IIntegrationTool> integrationTools,
    ILogger<GmailSkill> logger) : ISkill
{
    public string Name => "gmail-email";
    public string Description => "Gmail email management: read, summarize, prioritize, and manage emails.";

    public IReadOnlyList<string> Triggers =>
    [
        "check my email",
        "check emails",
        "read email",
        "email summary",
        "inbox summary",
        "email last 24 hours",
        "overnight emails",
        "morning email briefing",
        "important email",
        "unanswered email",
        "email I haven't responded to",
        "unreplied email",
        "summarize email thread",
        "email thread summary",
        "email from",
        "emails about",
        "prioritize email",
        "email priority"
    ];

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var text = context.Message.Text.ToLowerInvariant();

        logger.LogInformation("Gmail skill triggered with message: {Text}",
            text.Length > 80 ? text[..80] + "…" : text);

        // Route to the appropriate sub-operation based on message content
        if (ContainsAny(text, "overnight", "morning", "briefing", "last night", "while i slept"))
        {
            return await OvernightBriefingAsync(cancellationToken);
        }

        if (ContainsAny(text, "last 24 hours", "today", "recent"))
        {
            return await RecentSummaryAsync(TimeSpan.FromHours(24), cancellationToken);
        }

        if (ContainsAny(text, "last week", "past week", "this week"))
        {
            return await RecentSummaryAsync(TimeSpan.FromDays(7), cancellationToken);
        }

        if (ContainsAny(text, "important", "priority", "prioritize", "urgent"))
        {
            return await ImportantEmailsAsync(cancellationToken);
        }

        if (ContainsAny(text, "unanswered", "unreplied", "haven't responded", "not responded", "not replied"))
        {
            return await UnrepliedImportantAsync(cancellationToken);
        }

        if (ContainsAny(text, "thread", "conversation"))
        {
            return await ThreadSummaryHintAsync();
        }

        // Default: show recent inbox summary
        return await RecentSummaryAsync(TimeSpan.FromHours(12), cancellationToken);
    }

    private async Task<SkillResult> OvernightBriefingAsync(CancellationToken cancellationToken)
    {
        // Calculate "overnight" as since 6 PM yesterday (or 10 PM if it's early morning)
        var now = DateTime.UtcNow;
        var since = now.Hour < 12
            ? now.Date.AddDays(-1).AddHours(18) // since 6 PM yesterday
            : now.Date.AddHours(6);              // since 6 AM today

        var args = new Dictionary<string, object?> { ["since"] = since, ["maxResults"] = 50 };
        string messages = await InvokeToolFunctionAsync("gmail", "gmail_messages_since", args, cancellationToken);

        string response = $"""
            ### 📧 Overnight Email Briefing — {now:yyyy-MM-dd HH:mm} UTC

            **Emails since {since:yyyy-MM-dd HH:mm} UTC:**
            {messages}

            _Use "check important emails" to see only priority items, or "unreplied emails" to see what needs a response._
            """;

        return SkillResult.Ok(response);
    }

    private async Task<SkillResult> RecentSummaryAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - window;
        var args = new Dictionary<string, object?> { ["since"] = since, ["maxResults"] = 50 };
        string messages = await InvokeToolFunctionAsync("gmail", "gmail_messages_since", args, cancellationToken);

        string label = window.TotalHours switch
        {
            <= 24 => "Last 24 Hours",
            <= 168 => "Last 7 Days",
            _ => $"Last {window.TotalDays:F0} Days"
        };

        string response = $"""
            ### 📧 Email Summary — {label}

            {messages}
            """;

        return SkillResult.Ok(response);
    }

    private async Task<SkillResult> ImportantEmailsAsync(CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?> { ["query"] = "is:important is:unread", ["maxResults"] = 20 };
        string messages = await InvokeToolFunctionAsync("gmail", "gmail_search", args, cancellationToken);

        string response = $"""
            ### ⭐ Important Unread Emails

            {messages}

            _Reply with a message ID to get full details or take action._
            """;

        return SkillResult.Ok(response);
    }

    private async Task<SkillResult> UnrepliedImportantAsync(CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?> { ["days"] = 7 };
        string messages = await InvokeToolFunctionAsync("gmail", "gmail_unreplied_important", args, cancellationToken);

        return SkillResult.Ok($"""
            ### 📬 Important Emails Awaiting Your Reply (Last 7 Days)

            {messages}

            _These are emails from important senders that you haven't responded to yet._
            """);
    }

    private static Task<SkillResult> ThreadSummaryHintAsync()
    {
        return Task.FromResult(SkillResult.Ok(
            "To summarize an email thread, I need a thread ID. " +
            "Use \"check my email\" to list recent messages, then ask me to " +
            "\"summarize thread [THREAD_ID]\" for the one you're interested in."));
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

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
}

