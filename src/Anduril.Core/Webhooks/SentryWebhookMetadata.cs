using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Represents metadata from a Sentry webhook issue.
/// </summary>
public record SentryWebhookMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("severity")]
    public int? Severity { get; init; }

    [JsonPropertyName("severity_reason")]
    public string? SeverityReason { get; init; }

    [JsonPropertyName("initial_priority")]
    public int? InitialPriority { get; init; }
}

