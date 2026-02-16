using Anduril.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Calendar integration (Microsoft 365 / Outlook).
/// </summary>
public class CalendarToolOptions
{
    /// <summary>
    /// Gets or sets the Microsoft Graph access token.
    /// In production, this would use MSAL/OAuth2 for token acquisition.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra (Azure AD) Tenant ID.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the client ID for the application registration.
    /// </summary>
    public string? ClientId { get; set; }
}

/// <summary>
/// Integration tool for Microsoft 365 Calendar via Microsoft Graph.
/// Exposes AI-callable functions for listing events, creating meetings, etc.
/// </summary>
public class CalendarTool(IOptions<CalendarToolOptions> options, ILogger<CalendarTool> logger)
    : IIntegrationTool, IAsyncDisposable
{
    private readonly CalendarToolOptions _options = options.Value;
    private GraphServiceClient? _graphClient;

    public string Name => "calendar";
    public string Description => "Microsoft 365 Calendar integration for viewing and managing meetings.";
    public bool IsAvailable => _graphClient is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.AccessToken))
        {
            logger.LogWarning("Calendar AccessToken not configured. Calendar integration will be unavailable.");
            return Task.CompletedTask;
        }

        // TODO: Replace with proper MSAL-based authentication.
        // For scaffold, we accept a pre-obtained access token.
        // Note: Microsoft.Kiota.Abstractions does not provide a built-in static token provider,
        // so we use a custom TokenProvider implementation (see below).
        _graphClient = new GraphServiceClient(
            new Microsoft.Kiota.Abstractions.Authentication.BaseBearerTokenAuthenticationProvider(
                new TokenProvider(_options.AccessToken)));

        logger.LogInformation("Calendar integration initialized.");
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
        ];
    }

    private async Task<string> GetTodaysEventsAsync()
    {
        var client = GetGraphClient();
        var now = DateTime.UtcNow.Date;
        var end = now.AddDays(1);

        var events = await client.Me.CalendarView.GetAsync(config =>
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
        var client = GetGraphClient();
        var now = DateTime.UtcNow;
        var end = now.AddDays(days);

        var events = await client.Me.CalendarView.GetAsync(config =>
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

    private GraphServiceClient GetGraphClient() =>
        _graphClient ?? throw new InvalidOperationException("Calendar integration is not initialized.");

    public ValueTask DisposeAsync()
    {
        _graphClient?.Dispose();
        _graphClient = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Custom static token provider for Microsoft Graph.
    /// This is required because Microsoft.Kiota.Abstractions does not provide a built-in
    /// static/bearer token provider. In production, this should be replaced with MSAL-based
    /// token acquisition (e.g., using Azure.Identity.ClientSecretCredential or InteractiveBrowserCredential).
    /// </summary>
    private sealed class TokenProvider(string token)
        : Microsoft.Kiota.Abstractions.Authentication.IAccessTokenProvider
    {
        public Microsoft.Kiota.Abstractions.Authentication.AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }
}

