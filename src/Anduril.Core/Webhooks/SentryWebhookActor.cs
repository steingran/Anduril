using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Represents the actor that triggered a Sentry webhook event.
/// </summary>
public record SentryWebhookActor
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

