namespace Anduril.Host.Services;

/// <summary>
/// Abstraction for running arbitrary shell commands in the cloned repository.
/// Enables verification-command orchestration to be tested without spawning real processes.
/// </summary>
public interface IShellCommandRunner
{
    /// <summary>
    /// Executes a shell command and returns its standard output.
    /// </summary>
    Task<string> RunAsync(string workingDirectory, string command, CancellationToken cancellationToken = default);
}
