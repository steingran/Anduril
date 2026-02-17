using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Anduril.Integrations;

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
            AIFunctionFactory.Create(ListPullRequestsSinceAsync, "github_list_pull_requests_since",
                "List pull requests updated since a given date/time for a GitHub repository."),
            AIFunctionFactory.Create(ListIssuesSinceAsync, "github_list_issues_since",
                "List issues updated since a given date/time for a GitHub repository."),
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

    private async Task<string> ListPullRequestsSinceAsync(DateTime since, string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var request = new PullRequestRequest
        {
            State = ItemStateFilter.All,
            SortProperty = PullRequestSort.Updated,
            SortDirection = SortDirection.Descending
        };

        var apiOptions = new ApiOptions { PageSize = 100, PageCount = 10 };
        var allPrs = await client.PullRequest.GetAllForRepository(o, r, request, apiOptions);
        var sinceUtc = since.ToUniversalTime();
        var filtered = allPrs.Where(p => p.UpdatedAt >= new DateTimeOffset(sinceUtc, TimeSpan.Zero)).ToList();

        if (filtered.Count == 0)
            return $"No pull requests updated since {since:yyyy-MM-dd HH:mm}.";

        return string.Join("\n", filtered.Select(p =>
        {
            string state = p.Merged
                ? "merged"
                : p.State == ItemState.Open ? "open" : "closed";
            return $"#{p.Number}: {p.Title} ({state}, updated {p.UpdatedAt:yyyy-MM-dd HH:mm})";
        }));
    }

    private async Task<string> ListIssuesSinceAsync(DateTime since, string? owner = null, string? repo = null)
    {
        var client = GetClient();
        string o = owner ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = repo ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var sinceUtc = since.ToUniversalTime();
        var request = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            Since = new DateTimeOffset(sinceUtc, TimeSpan.Zero),
            SortProperty = IssueSort.Updated,
            SortDirection = SortDirection.Descending
        };

        var issues = await client.Issue.GetAllForRepository(o, r, request);
        // Octokit returns PRs as issues too — filter them out
        var filtered = issues.Where(i => i.PullRequest is null).ToList();

        if (filtered.Count == 0)
            return $"No issues updated since {since:yyyy-MM-dd HH:mm}.";

        return string.Join("\n", filtered.Select(i =>
        {
            string state = i.State == ItemState.Open ? "open" : "closed";
            return $"#{i.Number}: {i.Title} ({state}, by {i.User.Login}, updated {i.UpdatedAt:yyyy-MM-dd HH:mm})";
        }));
    }

    private GitHubClient GetClient() =>
        _client ?? throw new InvalidOperationException("GitHub integration is not initialized.");

    // Note: GitHubClient from Octokit does not implement IDisposable
}

