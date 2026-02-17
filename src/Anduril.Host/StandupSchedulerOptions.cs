namespace Anduril.Host;

/// <summary>
/// Configuration options for the standup scheduler background service.
/// </summary>
public class StandupSchedulerOptions
{
    /// <summary>
    /// Gets or sets the cron expression for when standups should be generated.
    /// Default: "25 9 * * 1,3" (Monday and Wednesday at 09:25).
    /// Only hour, minute, and day-of-week fields are used for scheduling.
    /// </summary>
    public string Schedule { get; set; } = "25 9 * * 1,3";

    /// <summary>
    /// Gets or sets the target communication platform ("slack" or "teams").
    /// </summary>
    public string TargetPlatform { get; set; } = "slack";

    /// <summary>
    /// Gets or sets the target channel or conversation ID to send the standup to.
    /// </summary>
    public string? TargetChannel { get; set; }

    /// <summary>
    /// Gets or sets whether the standup scheduler is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}

