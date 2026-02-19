namespace Anduril.Host;

/// <summary>
/// Configuration options for the Gmail email summary scheduler background service.
/// </summary>
public class GmailSchedulerOptions
{
    /// <summary>
    /// Gets or sets whether the Gmail scheduler is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the cron expression for when email summaries should be generated.
    /// Default: "0 7 * * 1,2,3,4,5" (weekdays at 07:00 UTC — morning briefing).
    /// Only hour, minute, and day-of-week fields are used for scheduling.
    /// </summary>
    public string Schedule { get; set; } = "0 7 * * 1,2,3,4,5";

    /// <summary>
    /// Gets or sets the target communication platform ("slack" or "teams").
    /// </summary>
    public string TargetPlatform { get; set; } = "slack";

    /// <summary>
    /// Gets or sets the target channel or conversation ID to send the email summary to.
    /// </summary>
    public string? TargetChannel { get; set; }

    /// <summary>
    /// Gets or sets how many hours of email history to include in the summary.
    /// Default: 12 (covers overnight emails for a morning briefing).
    /// </summary>
    public int SummaryHours { get; set; } = 12;
}

