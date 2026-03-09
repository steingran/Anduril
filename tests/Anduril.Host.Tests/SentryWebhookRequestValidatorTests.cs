using Anduril.Host;
using Anduril.Host.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Host.Tests;

public class SentryWebhookRequestValidatorTests
{
    [Test]
    public async Task Validate_WhenEnabledWithoutWebhookSecret_ReturnsForbidden()
    {
        var result = CreateValidator().Validate(CreateValidBody(), signature: null, new SentryBugfixOptions { Enabled = true });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(403);
    }

    [Test]
    public async Task Validate_WhenSignatureMissing_ReturnsUnauthorized()
    {
        var result = CreateValidator().Validate(CreateValidBody(), signature: null, new SentryBugfixOptions { Enabled = true, WebhookSecret = "secret" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task Validate_WhenSignatureIsInvalidHex_ReturnsUnauthorized()
    {
        var result = CreateValidator().Validate(CreateValidBody(), signature: "not-hex", new SentryBugfixOptions { Enabled = true, WebhookSecret = "secret" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task Validate_WhenSignatureDoesNotMatch_ReturnsUnauthorized()
    {
        var result = CreateValidator().Validate(CreateValidBody(), signature: Convert.ToHexString([1, 2, 3, 4]), new SentryBugfixOptions { Enabled = true, WebhookSecret = "secret" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task Validate_WhenPayloadIsInvalidJson_ReturnsBadRequest()
    {
        var body = Encoding.UTF8.GetBytes("{");
        var result = CreateValidator().Validate(body, ComputeSignature(body, "secret"), new SentryBugfixOptions { Enabled = true, WebhookSecret = "secret" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(400);
        await Assert.That(result.ErrorMessage).IsEqualTo("Invalid payload.");
    }

    [Test]
    public async Task Validate_WhenSignatureAndPayloadAreValid_ReturnsPayload()
    {
        var body = CreateValidBody();
        var result = CreateValidator().Validate(body, ComputeSignature(body, "secret"), new SentryBugfixOptions { Enabled = true, WebhookSecret = "secret" });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Payload).IsNotNull();
        await Assert.That(result.Payload!.Action).IsEqualTo("created");
        await Assert.That(result.Payload.Data.Issue.Id).IsEqualTo("12345");
    }

    private static SentryWebhookRequestValidator CreateValidator() =>
        new(NullLogger<SentryWebhookRequestValidator>.Instance);

    private static byte[] CreateValidBody() => Encoding.UTF8.GetBytes(
        """
        {
          "action": "created",
          "data": {
            "issue": {
              "id": "12345",
              "title": "NullReferenceException in Foo.Bar()",
              "count": "25",
              "userCount": 3
            }
          }
        }
        """);

    private static string ComputeSignature(byte[] body, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
}
