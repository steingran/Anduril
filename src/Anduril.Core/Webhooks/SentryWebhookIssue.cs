using System.Text.Json.Serialization;

namespace Anduril.Core.Webhooks;

/// <summary>
/// Represents a Sentry issue from a webhook payload.
/// </summary>
public record SentryWebhookIssue
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("shortId")]
    public string? ShortId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("culprit")]
    public string? Culprit { get; init; }

    [JsonPropertyName("level")]
    public string? Level { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("substatus")]
    public string? Substatus { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }

    [JsonPropertyName("count")]
    public string Count { get; init; } = "0";

    [JsonPropertyName("userCount")]
    public int UserCount { get; init; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; init; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; init; }

    [JsonPropertyName("project")]
    public SentryWebhookProject? Project { get; init; }

    [JsonPropertyName("metadata")]
    public SentryWebhookMetadata? Metadata { get; init; }

    [JsonPropertyName("firstSeen")]
    public string? FirstSeen { get; init; }

    [JsonPropertyName("lastSeen")]
    public string? LastSeen { get; init; }

    [JsonPropertyName("issueType")]
    public string? IssueType { get; init; }

    [JsonPropertyName("issueCategory")]
    public string? IssueCategory { get; init; }

    [JsonPropertyName("priority")]
    public string? Priority { get; init; }
}

