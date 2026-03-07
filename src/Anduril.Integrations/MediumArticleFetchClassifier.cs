using System.Net;

namespace Anduril.Integrations;

internal static class MediumArticleFetchClassifier
{
    public static MediumArticleFetchFailureReason ClassifyFailure(HttpStatusCode statusCode, string? html)
    {
        if (IsCloudflareChallenge(html))
            return MediumArticleFetchFailureReason.CloudflareChallenge;

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => MediumArticleFetchFailureReason.AuthenticationRequired,
            HttpStatusCode.Forbidden => MediumArticleFetchFailureReason.Forbidden,
            _ => MediumArticleFetchFailureReason.Unknown,
        };
    }

    public static bool IsCloudflareChallenge(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        return html.Contains("Enable JavaScript and cookies to continue", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("Checking if the site connection is secure", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cf-challenge", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cloudflare", StringComparison.OrdinalIgnoreCase) &&
               html.Contains("challenge", StringComparison.OrdinalIgnoreCase);
    }
}