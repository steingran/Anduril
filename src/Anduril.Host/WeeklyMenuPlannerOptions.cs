namespace Anduril.Host;

/// <summary>
/// Configuration options for weekly menu preference persistence and scheduled delivery.
/// </summary>
public sealed class WeeklyMenuPlannerOptions
{
    public bool Enabled { get; set; } = true;

    public string DatabasePath { get; set; } = "./sessions/weekly-menu-planner.db";

    public int SchedulerPollingIntervalMinutes { get; set; } = 5;
}