using Microsoft.Extensions.Logging;

namespace Anduril.AI.Detection;

/// <summary>
/// Detects whether Ollama is installed and running on the local machine.
/// Used during first-run setup to guide the user through local model configuration.
/// </summary>
public class OllamaDetector(ILogger<OllamaDetector> logger, HttpClient? httpClient = null)
    : IAsyncDisposable
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly bool _ownsHttpClient = httpClient is null;

    /// <summary>
    /// Checks if Ollama is running and accessible at the given endpoint.
    /// </summary>
    public async Task<bool> IsRunningAsync(string endpoint = "http://localhost:11434", CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{endpoint.TrimEnd('/')}/api/tags", cancellationToken);
            bool isRunning = response.IsSuccessStatusCode;
            logger.LogInformation("Ollama at {Endpoint}: {Status}", endpoint, isRunning ? "running" : "not responding");
            return isRunning;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ollama not reachable at {Endpoint}", endpoint);
            return false;
        }
    }

    /// <summary>
    /// Checks if Ollama is installed on the system (in PATH).
    /// </summary>
    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null) return false;

            await process.WaitForExitAsync(cancellationToken);
            bool installed = process.ExitCode == 0;
            logger.LogInformation("Ollama installed: {Installed}", installed);
            return installed;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ollama not found in PATH");
            return false;
        }
    }

    /// <summary>
    /// Gets the download URL for Ollama based on the current OS.
    /// </summary>
    public static string GetDownloadUrl()
    {
        if (OperatingSystem.IsWindows()) return "https://ollama.com/download/windows";
        if (OperatingSystem.IsMacOS()) return "https://ollama.com/download/mac";
        return "https://ollama.com/download/linux";
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}

