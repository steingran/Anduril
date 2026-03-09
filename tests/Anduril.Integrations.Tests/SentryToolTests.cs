using Anduril.Integrations;
using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class SentryToolTests
{
    [Test]
    public async Task InitializeAsync_WhenAuthTokenMissing_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<SentryTool>();
        var tool = CreateTool(
            (_, _) => throw new InvalidOperationException("HTTP should not be called."),
            new SentryToolOptions { AuthToken = null, Organization = "test-org", Project = "proj", BaseUrl = "https://sentry.example/api/0/" },
            logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Any(message => message.Contains("AuthToken", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenOrganizationMissing_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<SentryTool>();
        var tool = CreateTool(
            (_, _) => throw new InvalidOperationException("HTTP should not be called."),
            new SentryToolOptions { AuthToken = "token", Organization = null, Project = "proj", BaseUrl = "https://sentry.example/api/0/" },
            logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Any(message => message.Contains("Organization", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenBaseUrlInvalid_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<SentryTool>();
        var tool = CreateTool(
            (_, _) => throw new InvalidOperationException("HTTP should not be called."),
            new SentryToolOptions { AuthToken = "token", Organization = "test-org", Project = "proj", BaseUrl = "not a uri" },
            logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Any(message => message.Contains("BaseUrl", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_WhenConnectivityCheckFails_RemainsUnavailableAndLogsWarning()
    {
        var logger = new TestListLogger<SentryTool>();
        var tool = CreateTool(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)),
            logger: logger);

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsFalse();
        await Assert.That(logger.WarningMessages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task InitializeAsync_WhenConnectivityCheckSucceeds_BecomesAvailableAndSetsHeaders()
    {
        var requestedUri = string.Empty;
        var authHeader = string.Empty;
        var tool = CreateTool((request, _) =>
        {
            requestedUri = request.RequestUri!.ToString();
            authHeader = request.Headers.Authorization!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await tool.InitializeAsync();

        await Assert.That(tool.IsAvailable).IsTrue();
        await Assert.That(requestedUri).Contains("organizations/test-org/");
        await Assert.That(authHeader).IsEqualTo("Bearer token");
    }

    [Test]
    public async Task ListIssuesFunction_UsesConfiguredProjectAndLimit()
    {
        var requests = new List<string>();
        var tool = CreateTool((request, _) =>
        {
            requests.Add(request.RequestUri!.ToString());
            var responseBody = requests.Count == 1
                ? "{}"
                : "[{\"id\":\"1\",\"shortId\":\"PROJ-1\",\"title\":\"Null reference\",\"status\":\"unresolved\",\"count\":\"7\",\"userCount\":2}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        });
        await tool.InitializeAsync();

        var result = await InvokeFunctionAsync(tool, "sentry_list_issues", new Dictionary<string, object?>
        {
            ["project"] = "proj-a",
            ["limit"] = 5
        });

        await Assert.That(result).Contains("Unresolved Sentry issues for project 'proj-a':");
        await Assert.That(result).Contains("PROJ-1: Null reference");
        await Assert.That(requests[1]).Contains("projects/test-org/proj-a/issues/?query=is:unresolved&limit=5");
    }

    [Test]
    public async Task IssueFunctions_UseExpectedEndpoints()
    {
        var requests = new List<string>();
        var tool = CreateTool((request, _) =>
        {
            requests.Add(request.RequestUri!.ToString());
            var responseBody = requests.Count switch
            {
                1 => "{}",
                2 => "{\"id\":\"ISSUE-1\",\"shortId\":\"PROJ-1\",\"title\":\"Null reference\",\"status\":\"unresolved\",\"count\":\"7\",\"userCount\":2,\"permalink\":\"https://sentry.example/issues/ISSUE-1/\"}",
                _ => "{\"eventID\":\"EVENT-1\",\"title\":\"NullReferenceException\",\"message\":\"Object reference not set\",\"dateCreated\":\"2026-03-09T10:00:00Z\",\"tags\":[{\"key\":\"environment\",\"value\":\"production\"}]}"
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        });
        await tool.InitializeAsync();

        var issueResult = await InvokeFunctionAsync(tool, "sentry_get_issue", new Dictionary<string, object?> { ["issueId"] = "ISSUE-1" });
        var latestEventResult = await InvokeFunctionAsync(tool, "sentry_get_latest_event", new Dictionary<string, object?> { ["issueId"] = "ISSUE-1" });

        await Assert.That(requests[1]).Contains("issues/ISSUE-1/");
        await Assert.That(requests[2]).Contains("issues/ISSUE-1/events/latest/");
        await Assert.That(issueResult).Contains("Sentry issue PROJ-1");
        await Assert.That(issueResult).Contains("Permalink: https://sentry.example/issues/ISSUE-1/");
        await Assert.That(latestEventResult).Contains("Latest Sentry event for issue ISSUE-1");
        await Assert.That(latestEventResult).Contains("Environment: production");
    }

    private static SentryTool CreateTool(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
        SentryToolOptions? options = null,
        TestListLogger<SentryTool>? logger = null)
    {
        var httpClient = new HttpClient(new DelegateHttpMessageHandler(sendAsync));
        var configuredOptions = options ?? new SentryToolOptions
        {
            AuthToken = "token",
            Organization = "test-org",
            Project = "test-project",
            BaseUrl = "https://sentry.example/api/0/"
        };

        return new SentryTool(
            Options.Create(configuredOptions),
            logger ?? new TestListLogger<SentryTool>(),
            httpClient);
    }

    private static async Task<string> InvokeFunctionAsync(
        SentryTool tool,
        string functionName,
        IDictionary<string, object?> arguments)
    {
        var function = tool.GetFunctions().First(function => function.Name.Equals(functionName, StringComparison.Ordinal));
        var result = await function.InvokeAsync(new AIFunctionArguments(arguments), CancellationToken.None);
        return result?.ToString() ?? string.Empty;
    }
}
