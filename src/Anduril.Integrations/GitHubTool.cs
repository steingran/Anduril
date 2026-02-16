using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the GitHub integration.
/// </summary>
public class GitHubToolOptions
{
    /// <summary>
    /// Gets or sets the GitHub personal access token.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the default repository owner.
    /// </summary>
    public string? DefaultOwner { get; set; }

    /// <summary>
    /// Gets or sets the default repository name.
    /// </summary>
    public string? DefaultRepo { get; set; }
}

/// <summary>
/// Integration tool for GitHub, powered by Octokit.
/// Exposes AI-callable functions for listing issues, creating PRs, reviewing code, etc.
/// </summary>
public class GitHubTool(IOptions<GitHubToolOptions> options, ILogger<GitHubTool> logger)
    : IIntegrationTool
{
    private readonly GitHubToolOptions _options = options.Value;
    private GitHubClient? _client;

    public string Name => "github";
    public string Description => "GitHub integration for issues, pull requests, and code review.";
    public bool IsAvailable => _client is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.Token))
        {
            logger.LogWarning("GitHub token not configured. GitHub integration will be unavailable.");
            return Task.CompletedTask;
        }

        _client = new GitHubClient(new ProductHeaderValue("Anduril"))
        {
            Credentials = new Credentials(_options.Token)
        };

        logger.LogInformation("GitHub integration initialized.");
        return Task.CompletedTask;
    }

    public IReadOnlyList<AIFunction> GetFunctions()
    {
        return
        [
            AIFunctionFactory.Create(ListIssuesAsync, "github_list_issues",
                "List open issues for a GitHub repository."),
            AIFunctionFactory.Create(GetIssueAsync, "github_get_issue",
                "Get details of a specific GitHub issue by number."),
            AIFunctionFactory.Create(ListPullRequestsAsync, "github_list_pull_requests",
                "List open pull requests for a GitHub repository."),
            AIFunctionFactory.Create(GetPullRequestFilesAsync, "github_get_pr_files",
                "Get the list of changed files in a pull request."),
        ];
    }

    private async Task<string> ListIssuesAsync(string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var issues = await client.Issue.GetAllForRepository(o, r,
            new RepositoryIssueRequest { State = ItemStateFilter.Open });

        return string.Join("\n", issues.Select(i => $"#{i.Number}: {i.Title} (by {i.User.Login})"));
    }

    private async Task<string> GetIssueAsync(int number, string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var issue = await client.Issue.Get(o, r, number);
        return $"#{issue.Number}: {issue.Title}\nState: {issue.State}\nAuthor: {issue.User.Login}\n\n{issue.Body}";
    }

    private async Task<string> ListPullRequestsAsync(string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var prs = await client.PullRequest.GetAllForRepository(o, r);
        return string.Join("\n", prs.Select(p => $"#{p.Number}: {p.Title} ({p.Head.Ref} → {p.Base.Ref})"));
    }

    private async Task<string> GetPullRequestFilesAsync(int number, string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var files = await client.PullRequest.Files(o, r, number);
        return string.Join("\n", files.Select(f => $"{f.Status}: {f.FileName} (+{f.Additions} -{f.Deletions})"));
    }

    private GitHubClient GetClient() =>
        _client ?? throw new InvalidOperationException("GitHub integration is not initialized.");

    // Note: GitHubClient from Octokit does not implement IDisposable
}

