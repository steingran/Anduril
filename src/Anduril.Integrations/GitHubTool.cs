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
    private Task<string>? _currentUserLoginTask;

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
            AIFunctionFactory.Create(SearchOrgPullRequestsSinceAsync, "github_search_org_prs_since",
                "Search for pull requests involving the authenticated user across all repos in a GitHub organization since a given date."),
            AIFunctionFactory.Create(SearchOrgIssuesSinceAsync, "github_search_org_issues_since",
                "Search for issues involving the authenticated user across all repos in a GitHub organization since a given date."),
        ];
    }

    private async Task<string> ListIssuesAsync(string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var issues = await client.Issue.GetAllForRepository(o, r,
            new RepositoryIssueRequest { State = ItemStateFilter.Open });

        return string.Join("\n", issues.Select(i => $"#{i.Number}: {i.Title} (by {i.User.Login})"));
    }

    private async Task<string> GetIssueAsync(int number, string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var issue = await client.Issue.Get(o, r, number);
        return $"#{issue.Number}: {issue.Title}\nState: {issue.State}\nAuthor: {issue.User.Login}\n\n{issue.Body}";
    }

    private async Task<string> ListPullRequestsAsync(string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var prs = await client.PullRequest.GetAllForRepository(o, r);
        return string.Join("\n", prs.Select(p => $"#{p.Number}: {p.Title} ({p.Head.Ref} → {p.Base.Ref})"));
    }

    private async Task<string> GetPullRequestFilesAsync(int number, string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

        var files = await client.PullRequest.Files(o, r, number);
        return string.Join("\n", files.Select(f => $"{f.Status}: {f.FileName} (+{f.Additions} -{f.Deletions})"));
    }

    private async Task<string> ListPullRequestsSinceAsync(DateTime since, string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

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

    private async Task<string> ListIssuesSinceAsync(DateTime since, string owner = "", string repo = "")
    {
        var client = GetClient();
        string o = NullIfEmpty(owner) ?? _options.DefaultOwner ?? throw new ArgumentException("Repository owner is required.");
        string r = NullIfEmpty(repo) ?? _options.DefaultRepo ?? throw new ArgumentException("Repository name is required.");

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

    private async Task<string> SearchOrgPullRequestsSinceAsync(string organization, DateTime since)
    {
        if (string.IsNullOrWhiteSpace(organization))
            throw new ArgumentException("Organization is required.", nameof(organization));

        var sinceUtc = new DateTimeOffset(since.ToUniversalTime(), TimeSpan.Zero);
        var currentUserLogin = await GetCurrentUserLoginAsync();

        var request = new SearchIssuesRequest
        {
            Type = IssueTypeQualifier.PullRequest,
            Updated = DateRange.GreaterThanOrEquals(sinceUtc),
            SortField = IssueSearchSort.Updated,
            Order = SortDirection.Descending,
            Involves = currentUserLogin,
            User = organization
        };

        var pullRequests = await SearchIssuesAsync(request);

        if (pullRequests.Count == 0)
            return $"No pull requests found in org '{organization}' since {since:yyyy-MM-dd HH:mm}.";

        return string.Join("\n", pullRequests.Select(i =>
        {
            string state = i.State == ItemState.Open ? "open" : "closed";
            string repo = ExtractRepoName(i.HtmlUrl);
            return $"{repo}#{i.Number}: {i.Title} ({state}, updated {i.UpdatedAt:yyyy-MM-dd HH:mm})";
        }));
    }

    private async Task<string> SearchOrgIssuesSinceAsync(string organization, DateTime since)
    {
        if (string.IsNullOrWhiteSpace(organization))
            throw new ArgumentException("Organization is required.", nameof(organization));

        var sinceUtc = new DateTimeOffset(since.ToUniversalTime(), TimeSpan.Zero);
        var currentUserLogin = await GetCurrentUserLoginAsync();

        var request = new SearchIssuesRequest
        {
            Type = IssueTypeQualifier.Issue,
            Updated = DateRange.GreaterThanOrEquals(sinceUtc),
            SortField = IssueSearchSort.Updated,
            Order = SortDirection.Descending,
            Involves = currentUserLogin,
            User = organization
        };

        var issues = await SearchIssuesAsync(request);

        if (issues.Count == 0)
            return $"No issues found in org '{organization}' since {since:yyyy-MM-dd HH:mm}.";

        return string.Join("\n", issues.Select(i =>
        {
            string state = i.State == ItemState.Open ? "open" : "closed";
            string repo = ExtractRepoName(i.HtmlUrl);
            return $"{repo}#{i.Number}: {i.Title} ({state}, by {i.User.Login}, updated {i.UpdatedAt:yyyy-MM-dd HH:mm})";
        }));
    }

    private async Task<IReadOnlyList<Issue>> SearchIssuesAsync(SearchIssuesRequest request)
    {
        const int pageSize = 100;

        var client = GetClient();
        var issues = new List<Issue>();

        for (var page = 1; ; page++)
        {
            request.Page = page;
            request.PerPage = pageSize;

            var result = await client.Search.SearchIssues(request);
            if (result.Items.Count == 0)
                break;

            issues.AddRange(result.Items);

            if (issues.Count >= result.TotalCount || result.Items.Count < pageSize)
                break;
        }

        return issues;
    }

    private Task<string> GetCurrentUserLoginAsync()
    {
        var currentUserLoginTask = _currentUserLoginTask;
        if (currentUserLoginTask is not null)
            return currentUserLoginTask;

        currentUserLoginTask = LoadCurrentUserLoginAsync();
        var cachedTask = Interlocked.CompareExchange(ref _currentUserLoginTask, currentUserLoginTask, null);
        return cachedTask ?? currentUserLoginTask;
    }

    private async Task<string> LoadCurrentUserLoginAsync()
    {
        var currentUser = await GetClient().User.Current();
        if (string.IsNullOrWhiteSpace(currentUser.Login))
            throw new InvalidOperationException("Authenticated GitHub user login was not returned.");

        return currentUser.Login;
    }

    private static string ExtractRepoName(string? htmlUrl)
    {
        if (string.IsNullOrEmpty(htmlUrl) || !Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri))
            return "unknown";

        // HtmlUrl is like https://github.com/org/repo/pull/123 or /issues/456
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[1] : "unknown";
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private GitHubClient GetClient() =>
        _client ?? throw new InvalidOperationException("GitHub integration is not initialized.");

    // Note: GitHubClient from Octokit does not implement IDisposable
}

