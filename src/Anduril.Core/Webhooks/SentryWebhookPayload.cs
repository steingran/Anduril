using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Represents an incoming Sentry webhook payload for issue events.
/// </summary>
public record SentryWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public SentryWebhookData Data { get; init; } = new();

    [JsonPropertyName("actor")]
    public SentryWebhookActor? Actor { get; init; }
}

