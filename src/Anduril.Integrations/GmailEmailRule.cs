namespace Anduril.Integrations;

/// <summary>
/// Defines an email processing rule for the Gmail integration.
/// Rules are evaluated against incoming messages to trigger automated actions.
/// </summary>
public class GmailEmailRule
{
    /// <summary>
    /// Gets or sets the human-readable name of this rule.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the sender email address or pattern to match (e.g., "boss@company.com" or "@company.com").
    /// </summary>
    public string? FromFilter { get; set; }

    /// <summary>
    /// Gets or sets the subject line keyword or pattern to match.
    /// </summary>
    public string? SubjectFilter { get; set; }

    /// <summary>
    /// Gets or sets a keyword to search for in the email body.
    /// </summary>
    public string? BodyKeyword { get; set; }

    /// <summary>
    /// Gets or sets the action to perform when the rule matches.
    /// Supported actions: "notify", "auto-respond", "label", "extract-attachments".
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets the parameter for the action. Interpretation depends on the action:
    /// - notify: notification message template
    /// - auto-respond: response body text or template
    /// - label: label name to apply (e.g., "CATEGORY_PERSONAL", "Important", or a custom label)
    /// - extract-attachments: save path (overrides global AttachmentSavePath)
    /// </summary>
    public string? ActionParameter { get; set; }

    /// <summary>
    /// Gets or sets whether this rule is active.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

