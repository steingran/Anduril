using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Anduril.Host.Services;

/// <summary>
/// Runs git CLI commands by spawning a git process.
/// </summary>
public sealed partial class GitCommandRunner(ILogger<GitCommandRunner> logger) : IGitCommandRunner
{
    public async Task<string> RunAsync(string? workingDirectory, string arguments, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Running: git {Arguments}", RedactSensitiveArgs(arguments));

        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start git process: git {RedactSensitiveArgs(arguments)}");

        // Read stdout and stderr concurrently to prevent deadlocks when buffers fill
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        var output = outputTask.Result;
        var error = errorTask.Result;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git command failed (exit code {process.ExitCode}): {error}");

        return output;
    }

    private static string RedactSensitiveArgs(string arguments) =>
        SensitiveTokenPattern().Replace(arguments, "$1[REDACTED]");

    [GeneratedRegex(@"(Authorization:\s*Bearer\s+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveTokenPattern();
}

