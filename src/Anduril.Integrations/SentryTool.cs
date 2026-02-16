using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Sentry integration.
/// </summary>
public class SentryToolOptions
{
    /// <summary>
    /// Gets or sets the Sentry authentication token (for the API).
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Gets or sets the Sentry organization slug.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Gets or sets the default Sentry project slug.
    /// </summary>
    public string? Project { get; set; }

    /// <summary>
    /// Gets or sets the Sentry API base URL. Defaults to https://sentry.io/api/0/.
    /// </summary>
    public string BaseUrl { get; set; } = "https://sentry.io/api/0/";
}

/// <summary>
/// Integration tool for Sentry error monitoring.
/// Exposes AI-callable functions for listing issues, getting error details, etc.
/// Uses the Sentry REST API via HttpClient.
/// </summary>
public class SentryTool(IOptions<SentryToolOptions> options, ILogger<SentryTool> logger)
    : IIntegrationTool, IAsyncDisposable
{
    private readonly SentryToolOptions _options = options.Value;
    private HttpClient? _httpClient;

    public string Name => "sentry";
    public string Description => "Sentry error monitoring integration for triaging and investigating issues.";
    public bool IsAvailable => _httpClient is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.AuthToken))
        {
            logger.LogWarning("Sentry AuthToken not configured. Sentry integration will be unavailable.");
            return Task.CompletedTask;
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AuthToken);

        logger.LogInformation("Sentry integration initialized for organization '{Org}'.", _options.Organization);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AIFunction> GetFunctions()
    {
        return
        [
            AIFunctionFactory.Create(ListUnresolvedIssuesAsync, "sentry_list_issues",
                "List the most recent unresolved issues from Sentry."),
            AIFunctionFactory.Create(GetIssueDetailsAsync, "sentry_get_issue",
                "Get detailed information about a specific Sentry issue by ID."),
            AIFunctionFactory.Create(GetLatestEventAsync, "sentry_get_latest_event",
                "Get the latest event (occurrence) for a Sentry issue."),
        ];
    }

    private async Task<string> ListUnresolvedIssuesAsync(string? project = null, int limit = 10)
    {
        var client = GetClient();
        string org = _options.Organization ?? throw new InvalidOperationException("Sentry organization not configured.");
        string proj = project ?? _options.Project ?? throw new ArgumentException("Sentry project is required.");

        string response = await client.GetStringAsync(
            $"projects/{org}/{proj}/issues/?query=is:unresolved&limit={limit}");

        return response;
    }

    private async Task<string> GetIssueDetailsAsync(string issueId)
    {
        var client = GetClient();
        string response = await client.GetStringAsync($"issues/{issueId}/");
        return response;
    }

    private async Task<string> GetLatestEventAsync(string issueId)
    {
        var client = GetClient();
        string response = await client.GetStringAsync($"issues/{issueId}/events/latest/");
        return response;
    }

    private HttpClient GetClient() =>
        _httpClient ?? throw new InvalidOperationException("Sentry integration is not initialized.");

    public ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        return ValueTask.CompletedTask;
    }
}

