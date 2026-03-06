using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;

namespace Anduril.Skills.Tests;

public sealed class RecordingStandupIntegrationTool(string name) : IIntegrationTool
{
    public string Name => name;
    public string Description => "Recording integration tool for standup tests.";
    public bool IsAvailable => true;
    public List<string> InvokedFunctionNames { get; } = [];
    public List<string> RequestedOrganizations { get; } = [];

    public IReadOnlyList<AIFunction> GetFunctions() => Name switch
    {
        "github" =>
        [
            AIFunctionFactory.Create(ListPullRequestsSinceAsync, "github_list_pull_requests_since", "Test function"),
            AIFunctionFactory.Create(ListIssuesSinceAsync, "github_list_issues_since", "Test function"),
            AIFunctionFactory.Create(SearchOrgPullRequestsSinceAsync, "github_search_org_prs_since", "Test function"),
            AIFunctionFactory.Create(SearchOrgIssuesSinceAsync, "github_search_org_issues_since", "Test function")
        ],
        "office365-calendar" =>
        [
            AIFunctionFactory.Create(GetEventsSinceAsync, "calendar_events_since", "Test function"),
            AIFunctionFactory.Create(GetTodayAsync, "calendar_today", "Test function")
        ],
        _ => []
    };

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private string ListPullRequestsSinceAsync(DateTime since)
    {
        InvokedFunctionNames.Add("github_list_pull_requests_since");
        return "repo prs";
    }

    private string ListIssuesSinceAsync(DateTime since)
    {
        InvokedFunctionNames.Add("github_list_issues_since");
        return "repo issues";
    }

    private string SearchOrgPullRequestsSinceAsync(string organization, DateTime since)
    {
        InvokedFunctionNames.Add("github_search_org_prs_since");
        RequestedOrganizations.Add(organization);
        return "org prs";
    }

    private string SearchOrgIssuesSinceAsync(string organization, DateTime since)
    {
        InvokedFunctionNames.Add("github_search_org_issues_since");
        RequestedOrganizations.Add(organization);
        return "org issues";
    }

    private string GetEventsSinceAsync(DateTime since)
    {
        InvokedFunctionNames.Add("calendar_events_since");
        return "past meetings";
    }

    private string GetTodayAsync()
    {
        InvokedFunctionNames.Add("calendar_today");
        return "today meetings";
    }
}