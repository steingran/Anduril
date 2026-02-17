namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Office 365 Calendar integration (Microsoft 365 / Outlook).
/// Uses the client credentials flow (Azure.Identity) for automatic token acquisition and refresh.
/// </summary>
public class Office365CalendarToolOptions
{
    /// <summary>
    /// Gets or sets the Microsoft Entra (Azure AD) Tenant ID.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the application (client) ID from the app registration.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret from the app registration.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the user ID or user principal name (e.g. "user@contoso.com")
    /// whose calendar should be queried. Required because the client credentials flow
    /// does not have an interactive user context (/me is not available).
    /// </summary>
    public string? UserId { get; set; }
}

