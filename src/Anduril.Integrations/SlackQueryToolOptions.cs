namespace Anduril.Integrations;

/// <summary>
/// Configuration options for the Slack query integration.
/// Uses the shared Slack bot token from Communication:Slack and only stores query limits here.
/// </summary>
public sealed class SlackQueryToolOptions
{
    /// <summary>
    /// Gets or sets the default number of messages returned by Slack query functions.
    /// </summary>
    public int DefaultMessageLimit { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of messages a single tool call may return.
    /// </summary>
    public int MaximumMessageLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of messages to request from Slack per history page.
    /// </summary>
    public int SearchPageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of history pages scanned during a search.
    /// </summary>
    public int MaximumSearchPages { get; set; } = 10;
}