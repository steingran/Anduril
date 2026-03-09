using System.Diagnostics;

namespace Anduril.Host.Services;

/// <summary>
/// Runs shell commands for repository verification steps.
/// </summary>
public sealed class ShellCommandRunner(ILogger<ShellCommandRunner> logger) : IShellCommandRunner
{
    public async Task<string> RunAsync(string workingDirectory, string command, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Running verification command: {Command}", command);

        var (fileName, arguments) = GetShellInvocation(command);
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start verification command: {command}");

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Verification command failed (exit code {process.ExitCode}): {errorTask.Result}");

            return outputTask.Result;
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process may have already exited.
            }

            throw;
        }
    }

    private static (string FileName, string Arguments) GetShellInvocation(string command) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", $"/c {command}")
            : ("/bin/sh", $"-lc \"{command.Replace("\"", "\\\"")}\"");
}
