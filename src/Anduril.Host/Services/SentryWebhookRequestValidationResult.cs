using Anduril.Core.Webhooks;

namespace Anduril.Host.Services;

/// <summary>
/// Represents the outcome of validating and deserializing a Sentry webhook request.
/// </summary>
public sealed record SentryWebhookRequestValidationResult
{
    public bool IsValid => Payload is not null;

    public int? StatusCode { get; init; }

    public string? ErrorMessage { get; init; }

    public SentryWebhookPayload? Payload { get; init; }

    public static SentryWebhookRequestValidationResult Success(SentryWebhookPayload payload) =>
        new() { Payload = payload };

    public static SentryWebhookRequestValidationResult Failure(int statusCode, string? errorMessage = null) =>
        new() { StatusCode = statusCode, ErrorMessage = errorMessage };
}
