using System.Text;
using System.Text.Json;
using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for Sentry error monitoring.
/// Exposes AI-callable functions for listing issues, getting error details, etc.
/// Uses the Sentry REST API via HttpClient.
/// </summary>
public class SentryTool : IIntegrationTool, IAsyncDisposable
{
    public const string HttpClientName = nameof(SentryTool);

    private readonly SentryToolOptions _options;
    private readonly ILogger<SentryTool> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _providedHttpClient;
    private readonly bool _ownsHttpClient;
    private HttpClient? _httpClient;

    public SentryTool(
        IOptions<SentryToolOptions> options,
        ILogger<SentryTool> logger,
        IHttpClientFactory httpClientFactory)
        : this(options, logger, httpClientFactory, null)
    {
    }

    internal SentryTool(IOptions<SentryToolOptions> options, ILogger<SentryTool> logger, HttpClient? httpClient)
        : this(options, logger, null, httpClient)
    {
    }

    internal SentryTool(
        IOptions<SentryToolOptions> options,
        ILogger<SentryTool> logger,
        IHttpClientFactory? httpClientFactory,
        HttpClient? httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _providedHttpClient = httpClient;
        _ownsHttpClient = httpClient is null;
    }

    public string Name => "sentry";
    public string Description => "Sentry error monitoring integration for triaging and investigating issues.";
    public bool IsAvailable => _httpClient is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await DisposeOwnedClientAsync();

        if (string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            _logger.LogWarning("Sentry AuthToken not configured. Sentry integration will be unavailable.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Organization))
        {
            _logger.LogWarning("Sentry Organization not configured. Sentry integration will be unavailable.");
            return;
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _logger.LogWarning("Sentry BaseUrl '{BaseUrl}' is invalid. Sentry integration will be unavailable.", _options.BaseUrl);
            return;
        }

        var client = _providedHttpClient ?? _httpClientFactory?.CreateClient(HttpClientName) ?? new HttpClient();
        client.BaseAddress = baseUri;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AuthToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Anduril/1.0");

        try
        {
            using var response = await client.GetAsync($"organizations/{_options.Organization}/", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sentry integration auth/connectivity check failed for organization '{Org}' with status code {StatusCode}.",
                    _options.Organization,
                    (int)response.StatusCode);

                if (_ownsHttpClient)
                    client.Dispose();

                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sentry integration auth/connectivity check failed for organization '{Org}'.", _options.Organization);

            if (_ownsHttpClient)
                client.Dispose();

            return;
        }

        _httpClient = client;
        _logger.LogInformation("Sentry integration initialized for organization '{Org}'.", _options.Organization);
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

    private async Task<string> ListUnresolvedIssuesAsync(string project = "", int limit = 10)
    {
        var client = GetClient();
        string org = _options.Organization ?? throw new InvalidOperationException("Sentry organization not configured.");
        string proj = NullIfEmpty(project) ?? NullIfEmpty(_options.Project) ?? throw new ArgumentException("Sentry project is required.");

        string response = await client.GetStringAsync(
            $"projects/{org}/{proj}/issues/?query=is:unresolved&limit={limit}");

        return FormatIssueList(response, proj);
    }

    private async Task<string> GetIssueDetailsAsync(string issueId)
    {
        var client = GetClient();
        string response = await client.GetStringAsync($"issues/{issueId}/");
        return FormatIssueDetails(response, issueId);
    }

    private async Task<string> GetLatestEventAsync(string issueId)
    {
        var client = GetClient();
        string response = await client.GetStringAsync($"issues/{issueId}/events/latest/");
        return FormatLatestEvent(response, issueId);
    }

    private HttpClient GetClient() =>
        _httpClient ?? throw new InvalidOperationException("Sentry integration is not initialized.");

    private static string FormatIssueList(string response, string project)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return response;

            var builder = new StringBuilder();
            builder.AppendLine($"Unresolved Sentry issues for project '{project}':");

            int issueCount = 0;
            foreach (var issue in document.RootElement.EnumerateArray())
            {
                var issueReference = GetIssueReference(issue, "unknown-issue");
                var title = GetIssueTitle(issue) ?? "(no title provided)";
                var count = GetScalarString(issue, "count") ?? "unknown";
                var userCount = GetScalarString(issue, "userCount") ?? "unknown";
                var status = GetScalarString(issue, "status") ?? "unknown";
                var lastSeen = GetScalarString(issue, "lastSeen");

                builder
                    .Append("- ")
                    .Append(issueReference)
                    .Append(": ")
                    .Append(title)
                    .Append(" | status: ")
                    .Append(status)
                    .Append(" | count: ")
                    .Append(count)
                    .Append(" | users: ")
                    .Append(userCount);

                if (!string.IsNullOrWhiteSpace(lastSeen))
                    builder.Append(" | last seen: ").Append(lastSeen);

                builder.AppendLine();
                issueCount++;
            }

            if (issueCount == 0)
                return $"No unresolved Sentry issues were returned for project '{project}'.";

            return builder.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return response;
        }
    }

    private static string FormatIssueDetails(string response, string issueId)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return response;

            var issue = document.RootElement;
            var builder = new StringBuilder();

            builder.AppendLine($"Sentry issue {GetIssueReference(issue, issueId)}");
            AppendLine(builder, "Title", GetIssueTitle(issue));
            AppendLine(builder, "Status", GetScalarString(issue, "status"));
            AppendLine(builder, "Level", GetScalarString(issue, "level"));
            AppendLine(builder, "Project", GetNestedScalarString(issue, "project", "slug") ?? GetNestedScalarString(issue, "project", "name"));
            AppendLine(builder, "Culprit", GetScalarString(issue, "culprit"));
            AppendLine(builder, "Occurrences", GetScalarString(issue, "count"));
            AppendLine(builder, "Affected users", GetScalarString(issue, "userCount"));
            AppendLine(builder, "First seen", GetScalarString(issue, "firstSeen"));
            AppendLine(builder, "Last seen", GetScalarString(issue, "lastSeen"));
            AppendLine(builder, "Permalink", GetScalarString(issue, "permalink"));
            AppendTagLines(builder, issue);

            return builder.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return response;
        }
    }

    private static string FormatLatestEvent(string response, string issueId)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return response;

            var issueEvent = document.RootElement;
            var builder = new StringBuilder();

            builder.AppendLine($"Latest Sentry event for issue {issueId}");
            AppendLine(builder, "Event ID", GetScalarString(issueEvent, "eventID") ?? GetScalarString(issueEvent, "id"));
            AppendLine(builder, "Title", GetScalarString(issueEvent, "title"));
            AppendLine(builder, "Message", GetScalarString(issueEvent, "message") ?? GetFirstExceptionSummary(issueEvent));
            AppendLine(builder, "Platform", GetScalarString(issueEvent, "platform"));
            AppendLine(builder, "Date", GetScalarString(issueEvent, "dateCreated") ?? GetScalarString(issueEvent, "dateReceived"));
            AppendLine(builder, "Environment", GetTagValue(issueEvent, "environment"));
            AppendLine(builder, "Release", GetTagValue(issueEvent, "release"));
            AppendLine(builder, "User", GetTagValue(issueEvent, "user"));
            AppendTagLines(builder, issueEvent);

            return builder.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return response;
        }
    }

    private static void AppendLine(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(label).Append(": ").AppendLine(value);
    }

    private static void AppendTagLines(StringBuilder builder, JsonElement element)
    {
        if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return;

        int tagCount = 0;
        foreach (var tag in tags.EnumerateArray())
        {
            var key = GetScalarString(tag, "key");
            var value = GetScalarString(tag, "value");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;

            if (tagCount == 0)
                builder.AppendLine("Tags:");

            builder.Append("- ").Append(key).Append(": ").AppendLine(value);
            tagCount++;

            if (tagCount >= 5)
                break;
        }
    }

    private static string? GetIssueTitle(JsonElement issue) =>
        GetScalarString(issue, "title") ??
        GetNestedScalarString(issue, "metadata", "title") ??
        GetNestedScalarString(issue, "metadata", "value");

    private static string GetIssueReference(JsonElement issue, string fallbackIssueId) =>
        GetScalarString(issue, "shortId") ??
        GetScalarString(issue, "id") ??
        fallbackIssueId;

    private static string? GetTagValue(JsonElement element, string key)
    {
        if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var tag in tags.EnumerateArray())
        {
            var tagKey = GetScalarString(tag, "key");
            if (!string.Equals(tagKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            return GetScalarString(tag, "value");
        }

        return null;
    }

    private static string? GetFirstExceptionSummary(JsonElement issueEvent)
    {
        if (!issueEvent.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var entry in entries.EnumerateArray())
        {
            var entryType = GetScalarString(entry, "type");
            if (!string.Equals(entryType, "exception", StringComparison.OrdinalIgnoreCase))
                continue;

            var exceptionType = GetNestedScalarString(entry, "data", "values", "0", "type");
            var exceptionValue = GetNestedScalarString(entry, "data", "values", "0", "value");
            if (string.IsNullOrWhiteSpace(exceptionType) && string.IsNullOrWhiteSpace(exceptionValue))
                return null;

            if (string.IsNullOrWhiteSpace(exceptionType))
                return exceptionValue;

            if (string.IsNullOrWhiteSpace(exceptionValue))
                return exceptionType;

            return $"{exceptionType}: {exceptionValue}";
        }

        return null;
    }

    private static string? GetNestedScalarString(JsonElement element, params string[] propertyPath)
    {
        JsonElement current = element;
        foreach (var segment in propertyPath)
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var index))
                    return null;

                if (index < 0 || index >= current.GetArrayLength())
                    return null;

                current = current[index];
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }

        return ConvertJsonValueToString(current);
    }

    private static string? GetScalarString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue))
            return null;

        return ConvertJsonValueToString(propertyValue);
    }

    private static string? ConvertJsonValueToString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
            _httpClient?.Dispose();

        _httpClient = null;
        return ValueTask.CompletedTask;
    }

    private ValueTask DisposeOwnedClientAsync()
    {
        if (_ownsHttpClient)
            _httpClient?.Dispose();

        _httpClient = null;
        return ValueTask.CompletedTask;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

