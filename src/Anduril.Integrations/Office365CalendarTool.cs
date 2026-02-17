using Anduril.Core.Integrations;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace Anduril.Integrations;

/// <summary>
/// Integration tool for Office 365 Calendar via Microsoft Graph.
/// Uses the client credentials flow (Azure.Identity) for automatic token acquisition and refresh.
/// Queries a specific user's calendar via /users/{userId} since /me is not available
/// in the client credentials flow.
/// </summary>
public class Office365CalendarTool(IOptions<Office365CalendarToolOptions> options, ILogger<Office365CalendarTool> logger)
    : IIntegrationTool, IAsyncDisposable
{
    private readonly Office365CalendarToolOptions _options = options.Value;
    private GraphServiceClient? _graphClient;
    private string? _userId;

    public string Name => "office365-calendar";
    public string Description => "Office 365 Calendar integration for viewing and managing meetings via Microsoft Graph.";
    public bool IsAvailable => _graphClient is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.TenantId) ||
            string.IsNullOrEmpty(_options.ClientId) ||
            string.IsNullOrEmpty(_options.ClientSecret))
        {
            logger.LogWarning(
                "Office 365 Calendar credentials not fully configured (TenantId, ClientId, and ClientSecret are all required). " +
                "Calendar integration will be unavailable.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(_options.UserId))
        {
            logger.LogWarning(
                "Office 365 Calendar UserId not configured. " +
                "A user ID or UPN (e.g. user@contoso.com) is required to query a specific user's calendar. " +
                "Calendar integration will be unavailable.");
            return Task.CompletedTask;
        }

        var credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        _graphClient = new GraphServiceClient(
            credential,
            ["https://graph.microsoft.com/.default"]);
        _userId = _options.UserId;

        logger.LogInformation("Office 365 Calendar integration initialized for user '{UserId}'.", _userId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AIFunction> GetFunctions()
    {
        return
        [
            AIFunctionFactory.Create(GetTodaysEventsAsync, "calendar_today",
                "Get today's calendar events."),
            AIFunctionFactory.Create(GetUpcomingEventsAsync, "calendar_upcoming",
                "Get upcoming calendar events for the next N days."),
            AIFunctionFactory.Create(GetEventsSinceAsync, "calendar_events_since",
                "Get calendar events that occurred since a given date/time."),
        ];
    }

    private async Task<string> GetTodaysEventsAsync()
    {
        var (client, userId) = GetGraphClient();
        var now = DateTime.UtcNow.Date;
        var end = now.AddDays(1);

        var events = await client.Users[userId].CalendarView.GetAsync(config =>
        {
            config.QueryParameters.StartDateTime = now.ToString("o");
            config.QueryParameters.EndDateTime = end.ToString("o");
            config.QueryParameters.Orderby = ["start/dateTime"];
            config.QueryParameters.Top = 20;
        });

        if (events?.Value is null || events.Value.Count == 0)
            return "No events scheduled for today.";

        return string.Join("\n", events.Value.Select(e =>
            $"{e.Start?.DateTime} - {e.End?.DateTime}: {e.Subject} ({e.Location?.DisplayName})"));
    }

    private async Task<string> GetUpcomingEventsAsync(int days = 7)
    {
        var (client, userId) = GetGraphClient();
        var now = DateTime.UtcNow;
        var end = now.AddDays(days);

        var events = await client.Users[userId].CalendarView.GetAsync(config =>
        {
            config.QueryParameters.StartDateTime = now.ToString("o");
            config.QueryParameters.EndDateTime = end.ToString("o");
            config.QueryParameters.Orderby = ["start/dateTime"];
            config.QueryParameters.Top = 50;
        });

        if (events?.Value is null || events.Value.Count == 0)
            return $"No events in the next {days} days.";

        return string.Join("\n", events.Value.Select(e =>
            $"{e.Start?.DateTime} - {e.End?.DateTime}: {e.Subject}"));
    }

    private async Task<string> GetEventsSinceAsync(DateTime since)
    {
        var (client, userId) = GetGraphClient();
        var now = DateTime.UtcNow;

        var events = await client.Users[userId].CalendarView.GetAsync(config =>
        {
            config.QueryParameters.StartDateTime = since.ToUniversalTime().ToString("o");
            config.QueryParameters.EndDateTime = now.ToString("o");
            config.QueryParameters.Orderby = ["start/dateTime"];
            config.QueryParameters.Top = 50;
        });

        if (events?.Value is null || events.Value.Count == 0)
            return $"No calendar events since {since:yyyy-MM-dd HH:mm}.";

        return string.Join("\n", events.Value.Select(e =>
            $"{e.Start?.DateTime} - {e.End?.DateTime}: {e.Subject} ({e.Location?.DisplayName})"));
    }

    private (GraphServiceClient Client, string UserId) GetGraphClient() =>
        _graphClient is not null && _userId is not null
            ? (_graphClient, _userId)
            : throw new InvalidOperationException("Office 365 Calendar integration is not initialized.");

    public ValueTask DisposeAsync()
    {
        _graphClient?.Dispose();
        _graphClient = null;
        return ValueTask.CompletedTask;
    }
}

