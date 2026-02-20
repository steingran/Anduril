using Anduril.Integrations;
using Microsoft.Extensions.Options;
using Octokit;

namespace Anduril.Host.Services;

/// <summary>
/// Creates GitHub pull requests using the Octokit library.
/// </summary>
public sealed class OctokitPullRequestCreator(
    IOptions<GitHubToolOptions> gitHubOptions,
    ILogger<OctokitPullRequestCreator> logger) : IPullRequestCreator
{
    public async Task<string> CreateAsync(
        string owner, string repo, string branchName, string baseBranch, string title, string body,
        CancellationToken cancellationToken = default)
    {
        var token = gitHubOptions.Value.Token
            ?? throw new InvalidOperationException("GitHub token is required for creating pull requests.");

        var client = new GitHubClient(new ProductHeaderValue("Anduril-Bot"))
        {
            Credentials = new Credentials(token)
        };

        logger.LogDebug("Creating PR in {Owner}/{Repo} from branch {Branch} → {BaseBranch}...", owner, repo, branchName, baseBranch);

        var pr = await client.PullRequest.Create(owner, repo, new NewPullRequest(title, branchName, baseBranch)
        {
            Body = body
        });

        return pr.HtmlUrl;
    }
}

