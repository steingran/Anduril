using System.Net;

namespace Anduril.Integrations.Tests;

public class MediumArticleFetchClassifierTests
{
    [Test]
    public async Task IsCloudflareChallenge_WhenChallengePhrasePresent_ReturnsTrue()
    {
        var result = MediumArticleFetchClassifier.IsCloudflareChallenge(
            "<html><body>Enable JavaScript and cookies to continue</body></html>");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsCloudflareChallenge_WhenNormalMediumHtmlPresent_ReturnsFalse()
    {
        var result = MediumArticleFetchClassifier.IsCloudflareChallenge(
            "<html><head><meta name=\"generator\" content=\"Medium\" /></head></html>");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ClassifyFailure_WhenForbiddenWithoutChallenge_ReturnsForbidden()
    {
        var result = MediumArticleFetchClassifier.ClassifyFailure(HttpStatusCode.Forbidden, "<html><body>forbidden</body></html>");

        await Assert.That(result).IsEqualTo(MediumArticleFetchFailureReason.Forbidden);
    }

    [Test]
    public async Task ClassifyFailure_WhenUnauthorized_ReturnsAuthenticationRequired()
    {
        var result = MediumArticleFetchClassifier.ClassifyFailure(HttpStatusCode.Unauthorized, string.Empty);

        await Assert.That(result).IsEqualTo(MediumArticleFetchFailureReason.AuthenticationRequired);
    }
}