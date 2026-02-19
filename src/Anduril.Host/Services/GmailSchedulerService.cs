using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Services;

/// <summary>
/// Background service that triggers the gmail-email compiled skill on a configurable
/// schedule and sends the email summary to a configured communication channel.
/// Follows the same pattern as <see cref="StandupSchedulerService"/>.
/// </summary>
public sealed class GmailSchedulerService(
    IOptions<GmailSchedulerOptions> options,
    CompiledSkillRunner compiledRunner,
    IEnumerable<ICommunicationAdapter> adapters,
    ILogger<GmailSchedulerService> logger) : BackgroundService
{
    private readonly GmailSchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Gmail scheduler is disabled.");
            return;
        }

        if (string.IsNullOrEmpty(_options.TargetChannel))
        {
            logger.LogWarning("Gmail scheduler enabled but no TargetChannel configured. Disabling.");
            return;
        }

        if (!StandupSchedulerService.TryParseCronSchedule(_options.Schedule, out int minute, out int hour, out DayOfWeek[] days))
        {
            logger.LogError("Invalid cron schedule '{Schedule}'. Gmail scheduler will not run.", _options.Schedule);
            return;
        }

        logger.LogInformation(
            "Gmail scheduler started. Schedule: {Schedule} (days: {Days}, time: {Hour}:{Minute:D2})",
            _options.Schedule, string.Join(", ", days), hour, minute);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = StandupSchedulerService.CalculateNextRun(now, hour, minute, days);
            var delay = nextRun - now;

            logger.LogDebug("Next Gmail summary scheduled for {NextRun:yyyy-MM-dd HH:mm} UTC (in {Delay})",
                nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await GenerateAndSendSummaryAsync(stoppingToken);
        }
    }

    private async Task GenerateAndSendSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Generating scheduled Gmail summary...");

            var context = new SkillContext
            {
                Message = new IncomingMessage
                {
                    Id = $"gmail-summary-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Text = "overnight emails morning email briefing",
                    UserId = "scheduler",
                    ChannelId = _options.TargetChannel!,
                    Platform = _options.TargetPlatform
                },
                UserId = "scheduler",
                ChannelId = _options.TargetChannel
            };

            var result = await compiledRunner.ExecuteAsync("gmail-email", context, cancellationToken);

            if (!result.Success)
            {
                logger.LogError("Gmail skill failed: {Error}", result.ErrorMessage);
                return;
            }

            var adapter = adapters.FirstOrDefault(a =>
                a.Platform.Equals(_options.TargetPlatform, StringComparison.OrdinalIgnoreCase)
                && a.IsConnected);

            if (adapter is null)
            {
                logger.LogWarning(
                    "No connected adapter found for platform '{Platform}'. Gmail summary not sent.",
                    _options.TargetPlatform);
                return;
            }

            await adapter.SendMessageAsync(new OutgoingMessage
            {
                Text = result.Response,
                ChannelId = _options.TargetChannel!
            }, cancellationToken);

            logger.LogInformation("Gmail summary sent to {Platform}/{Channel}",
                _options.TargetPlatform, _options.TargetChannel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate or send scheduled Gmail summary");
        }
    }
}

