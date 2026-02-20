using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Services;

/// <summary>
/// Runs the Augment Code CLI (auggie) by spawning a process and piping the prompt to stdin.
/// </summary>
public sealed class AuggieCliRunner(
    IOptions<SentryBugfixOptions> options,
    ILogger<AuggieCliRunner> logger) : IAuggieCliRunner
{
    private readonly SentryBugfixOptions _options = options.Value;

    public async Task RunAsync(string workingDirectory, string prompt, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMinutes(_options.AuggieTimeoutMinutes);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo(_options.AugmentCliPath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start auggie CLI process.");

        try
        {
            // Pipe the prompt to auggie's stdin
            await process.StandardInput.WriteAsync(prompt.AsMemory(), cts.Token);
            await process.StandardInput.FlushAsync(cts.Token);
            process.StandardInput.Close();

            // Read stdout and stderr concurrently to prevent deadlocks when buffers fill
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cts.Token);

            logger.LogDebug("Auggie output: {Output}", outputTask.Result);

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Auggie exited with code {process.ExitCode}. Stderr: {errorTask.Result}");
        }
        catch (OperationCanceledException)
        {
            // Kill the entire process tree to prevent orphaned child processes
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process may have already exited
            }

            // Distinguish between external cancellation (e.g., shutdown) and timeout
            if (cancellationToken.IsCancellationRequested)
                throw;

            throw new TimeoutException(
                $"Auggie timed out after {_options.AuggieTimeoutMinutes} minutes and was terminated.");
        }
    }
}

