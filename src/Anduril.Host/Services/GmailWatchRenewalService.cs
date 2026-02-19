using Anduril.Core.Integrations;
using Anduril.Integrations;

namespace Anduril.Host.Services;

/// <summary>
/// Background service that renews the Gmail push notification watch.
/// Gmail watches expire after 7 days and must be renewed before expiration.
/// This service renews every 6 days to ensure continuous coverage.
/// </summary>
public sealed class GmailWatchRenewalService(
    IEnumerable<IIntegrationTool> integrationTools,
    ILogger<GmailWatchRenewalService> logger) : BackgroundService
{
    /// <summary>
    /// Renewal interval: 6 days (Gmail watch expires after 7 days).
    /// </summary>
    private static readonly TimeSpan RenewalInterval = TimeSpan.FromDays(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var gmailTool = integrationTools
            .OfType<GmailTool>()
            .FirstOrDefault();

        if (gmailTool is null || !gmailTool.IsAvailable)
        {
            logger.LogInformation("Gmail integration not available. Watch renewal service disabled.");
            return;
        }

        if (string.IsNullOrEmpty(gmailTool.PubSubTopic))
        {
            logger.LogInformation("Gmail Pub/Sub topic not configured. Watch renewal service disabled.");
            return;
        }

        logger.LogInformation("Gmail watch renewal service started. Renewal interval: {Interval}", RenewalInterval);

        // Set up the initial watch
        await RenewWatchAsync(gmailTool);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RenewalInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RenewWatchAsync(gmailTool);
        }
    }

    private async Task RenewWatchAsync(GmailTool gmailTool)
    {
        try
        {
            var result = await gmailTool.SetupWatchAsync();
            logger.LogInformation("Gmail watch renewed: {Result}", result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to renew Gmail watch. Will retry at next interval.");
        }
    }
}

