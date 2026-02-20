using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Contains the issue data from a Sentry webhook.
/// </summary>
public record SentryWebhookData
{
    [JsonPropertyName("issue")]
    public SentryWebhookIssue Issue { get; init; } = new();
}

