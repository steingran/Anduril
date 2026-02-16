using Velopack;

namespace Anduril.Host;

/// <summary>
/// Background service that periodically checks for application updates using Velopack.
/// On finding a new version, it downloads and prepares the update for the next restart.
/// </summary>
public class UpdateService : BackgroundService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;

    public UpdateService(ILogger<UpdateService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        int intervalMinutes = configuration.GetValue("Updates:CheckIntervalMinutes", 60);

        // Clamp to a sensible minimum to prevent tight-loop or invalid delays
        if (intervalMinutes < 1)
        {
            _logger.LogWarning(
                "Invalid update check interval ({Interval} minutes). Clamping to minimum of 1 minute.",
                intervalMinutes);
            intervalMinutes = 1;
        }

        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? updateUrl = _configuration.GetValue<string>("Updates:Url");

        if (string.IsNullOrEmpty(updateUrl))
        {
            _logger.LogInformation("Update URL not configured. Auto-update is disabled.");
            return;
        }

        _logger.LogInformation("Update service started. Checking every {Interval} minutes.", _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(updateUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update check failed. Will retry at next interval.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckForUpdatesAsync(string updateUrl)
    {
        // Note: UpdateManager does not implement IDisposable in Velopack 0.0.1298
        var mgr = new UpdateManager(updateUrl);

        if (!mgr.IsInstalled)
        {
            _logger.LogDebug("Application is not installed via Velopack (dev mode). Skipping update check.");
            return;
        }

        var updateInfo = await mgr.CheckForUpdatesAsync();

        if (updateInfo is null)
        {
            _logger.LogDebug("No updates available.");
            return;
        }

        _logger.LogInformation("Update available: {Version}. Downloading...", updateInfo.TargetFullRelease.Version);
        await mgr.DownloadUpdatesAsync(updateInfo);
        _logger.LogInformation("Update downloaded. It will be applied on next restart.");

        // Optionally apply and restart:
        // mgr.ApplyUpdatesAndRestart(updateInfo);
    }
}

