using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Services;

/// <summary>
/// Background service that triggers the standup-helper compiled skill on a configurable
/// schedule (default: Monday and Wednesday at 09:25) and sends the result to a
/// configured communication channel.
/// </summary>
public sealed class StandupSchedulerService(
    IOptions<StandupSchedulerOptions> options,
    CompiledSkillRunner compiledRunner,
    IEnumerable<ICommunicationAdapter> adapters,
    ILogger<StandupSchedulerService> logger) : BackgroundService
{
    private readonly StandupSchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Standup scheduler is disabled.");
            return;
        }

        if (string.IsNullOrEmpty(_options.TargetChannel))
        {
            logger.LogWarning("Standup scheduler enabled but no TargetChannel configured. Disabling.");
            return;
        }

        if (!TryParseCronSchedule(_options.Schedule, out int minute, out int hour, out DayOfWeek[] days))
        {
            logger.LogError("Invalid cron schedule '{Schedule}'. Standup scheduler will not run.", _options.Schedule);
            return;
        }

        logger.LogInformation(
            "Standup scheduler started. Schedule: {Schedule} (days: {Days}, time: {Hour}:{Minute:D2})",
            _options.Schedule, string.Join(", ", days), hour, minute);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = CalculateNextRun(now, hour, minute, days);
            var delay = nextRun - now;

            logger.LogDebug("Next standup scheduled for {NextRun:yyyy-MM-dd HH:mm} UTC (in {Delay})",
                nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await GenerateAndSendStandupAsync(stoppingToken);
        }
    }

    private async Task GenerateAndSendStandupAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Generating scheduled standup...");

            var properties = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(_options.GitHubOrganization))
                properties["GitHubOrganization"] = _options.GitHubOrganization;

            var context = new SkillContext
            {
                Message = new IncomingMessage
                {
                    Id = $"standup-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Text = "generate standup",
                    UserId = "scheduler",
                    ChannelId = _options.TargetChannel!,
                    Platform = _options.TargetPlatform
                },
                UserId = "scheduler",
                ChannelId = _options.TargetChannel,
                Properties = properties
            };

            var result = await compiledRunner.ExecuteAsync("standup-helper", context, cancellationToken);

            if (!result.Success)
            {
                logger.LogError("Standup skill failed: {Error}", result.ErrorMessage);
                return;
            }

            var adapter = adapters.FirstOrDefault(a =>
                a.Platform.Equals(_options.TargetPlatform, StringComparison.OrdinalIgnoreCase)
                && a.IsConnected);

            if (adapter is null)
            {
                logger.LogWarning(
                    "No connected adapter found for platform '{Platform}'. Standup not sent.",
                    _options.TargetPlatform);
                return;
            }

            await adapter.SendMessageAsync(new OutgoingMessage
            {
                Text = result.Response,
                ChannelId = _options.TargetChannel!
            }, cancellationToken);

            logger.LogInformation("Standup sent to {Platform}/{Channel}", _options.TargetPlatform, _options.TargetChannel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate or send scheduled standup");
        }
    }

    /// <summary>
    /// Parses a simplified cron expression (minute hour * * days-of-week).
    /// Supports numeric day-of-week values: 0=Sunday, 1=Monday, ..., 6=Saturday.
    /// </summary>
    internal static bool TryParseCronSchedule(
        string cron, out int minute, out int hour, out DayOfWeek[] daysOfWeek)
    {
        minute = 0;
        hour = 0;
        daysOfWeek = [];

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return false;

        if (!int.TryParse(parts[0], out minute) || minute is < 0 or > 59)
            return false;

        if (!int.TryParse(parts[1], out hour) || hour is < 0 or > 23)
            return false;

        // Parse day-of-week field (comma-separated)
        var dayParts = parts[4].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var days = new List<DayOfWeek>();
        foreach (var d in dayParts)
        {
            if (!int.TryParse(d, out int dayNum) || dayNum is < 0 or > 6)
                return false;
            days.Add((DayOfWeek)dayNum);
        }

        daysOfWeek = [.. days];
        return daysOfWeek.Length > 0;
    }

    /// <summary>
    /// Calculates the next occurrence of the scheduled time from the given instant.
    /// </summary>
    internal static DateTime CalculateNextRun(DateTime now, int hour, int minute, DayOfWeek[] daysOfWeek)
    {
        // Start from today and look up to 7 days ahead
        for (int offset = 0; offset <= 7; offset++)
        {
            var candidate = now.Date.AddDays(offset).AddHours(hour).AddMinutes(minute);

            if (candidate <= now)
                continue;

            if (daysOfWeek.Contains(candidate.DayOfWeek))
                return candidate;
        }

        // Fallback (should never reach here with valid config)
        return now.AddDays(1);
    }
}

