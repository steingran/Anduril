namespace Anduril.Host.Services;

/// <summary>
/// Abstraction for creating pull requests on a Git hosting platform (e.g., GitHub).
/// Enables testing of the bugfix pipeline without requiring real API calls.
/// </summary>
public interface IPullRequestCreator
{
    /// <summary>
    /// Finds an existing open pull request for the given source branch and returns its HTML URL.
    /// Returns null when no open pull request exists.
    /// </summary>
    Task<string?> FindOpenAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a pull request and returns its HTML URL.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="branchName">The source branch name.</param>
    /// <param name="baseBranch">The target branch the PR merges into (e.g., "main").</param>
    /// <param name="title">The pull request title.</param>
    /// <param name="body">The pull request body (markdown).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTML URL of the created pull request.</returns>
    Task<string> CreateAsync(string owner, string repo, string branchName, string baseBranch, string title, string body, CancellationToken cancellationToken = default);
}

