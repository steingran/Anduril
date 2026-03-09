using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anduril.Core.Webhooks;

namespace Anduril.Host.Services;

/// <summary>
/// Validates Sentry webhook signatures and deserializes webhook payloads.
/// </summary>
public sealed class SentryWebhookRequestValidator(ILogger<SentryWebhookRequestValidator> logger)
{
    public SentryWebhookRequestValidationResult Validate(
        ReadOnlyMemory<byte> body,
        string? signature,
        SentryBugfixOptions options)
    {
        var secret = options.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (options.Enabled)
            {
                logger.LogError("Sentry bugfix is enabled but WebhookSecret is not configured. Rejecting request.");
                return SentryWebhookRequestValidationResult.Failure(403);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(signature))
            {
                logger.LogWarning("Sentry webhook missing sentry-hook-signature header.");
                return SentryWebhookRequestValidationResult.Failure(401);
            }

            byte[] signatureBytes;
            try
            {
                signatureBytes = Convert.FromHexString(signature);
            }
            catch (FormatException)
            {
                logger.LogWarning("Sentry webhook signature is not valid hex.");
                return SentryWebhookRequestValidationResult.Failure(401);
            }

            var computedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body.Span);
            if (!CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes))
            {
                logger.LogWarning("Sentry webhook signature mismatch.");
                return SentryWebhookRequestValidationResult.Failure(401);
            }
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SentryWebhookPayload>(body.Span);
            if (payload is null)
            {
                logger.LogWarning("Received null Sentry webhook payload.");
                return SentryWebhookRequestValidationResult.Failure(400, "Invalid payload.");
            }

            return SentryWebhookRequestValidationResult.Success(payload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize Sentry webhook payload.");
            return SentryWebhookRequestValidationResult.Failure(400, "Invalid payload.");
        }
    }
}