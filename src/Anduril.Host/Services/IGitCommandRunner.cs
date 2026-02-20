namespace Anduril.Host.Services;

/// <summary>
/// Abstraction for running git CLI commands.
/// Enables testing of orchestration logic without requiring a real git binary.
/// </summary>
public interface IGitCommandRunner
{
    /// <summary>
    /// Executes a git command with the given arguments.
    /// </summary>
    /// <param name="workingDirectory">The working directory, or null for commands that don't need one (e.g., clone).</param>
    /// <param name="arguments">The git command arguments (e.g., "clone --depth 50 ...").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The standard output of the git command.</returns>
    Task<string> RunAsync(string? workingDirectory, string arguments, CancellationToken cancellationToken = default);
}

