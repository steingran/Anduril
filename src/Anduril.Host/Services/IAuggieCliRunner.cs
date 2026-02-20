namespace Anduril.Host.Services;

/// <summary>
/// Abstraction for invoking the Augment Code CLI (auggie).
/// Enables testing of the bugfix pipeline without requiring the real auggie binary.
/// </summary>
public interface IAuggieCliRunner
{
    /// <summary>
    /// Runs the auggie CLI with the given prompt piped to stdin.
    /// </summary>
    /// <param name="workingDirectory">The repository working directory where auggie should operate.</param>
    /// <param name="prompt">The prompt describing the bug to fix, piped to auggie's stdin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RunAsync(string workingDirectory, string prompt, CancellationToken cancellationToken = default);
}

