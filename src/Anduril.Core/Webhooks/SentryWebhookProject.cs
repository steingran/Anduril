using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Represents the project metadata from a Sentry webhook issue.
/// </summary>
public record SentryWebhookProject
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }
}

