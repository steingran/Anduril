using System.Text.RegularExpressions;

namespace Anduril.Integrations;

/// <summary>
/// Detects whether a URL or HTML document appears to be a Medium-hosted article.
/// </summary>
internal static class MediumUrlDetector
{
    private static readonly Regex CanonicalLinkRegex = new(
        "<link[^>]+rel=[\"']canonical[\"'][^>]+href=[\"'](?<url>[^\"']+)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsLikelyMediumArticle(Uri uri, string html)
    {
        if (IsMediumHost(uri.Host))
            return true;

        var canonical = ExtractCanonicalUrl(html, uri);
        if (canonical is not null && IsMediumHost(canonical.Host))
            return true;

        return html.Contains("name=\"generator\" content=\"Medium\"", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("name='generator' content='Medium'", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("com.medium.reader", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cdn-client.medium.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPaywalled(string html) =>
        html.Contains("Member-only story", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("member only story", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("meteredContent", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("\"postAccess\":\"LOCKED\"", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("\"isLockedPreview\":true", StringComparison.OrdinalIgnoreCase);

    public static Uri? ExtractCanonicalUrl(string html, Uri fallbackUri)
    {
        var match = CanonicalLinkRegex.Match(html);
        if (!match.Success)
            return null;

        return Uri.TryCreate(fallbackUri, match.Groups["url"].Value, out var canonicalUri)
            ? canonicalUri
            : null;
    }

    private static bool IsMediumHost(string host) =>
        host.Equals("medium.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".medium.com", StringComparison.OrdinalIgnoreCase);
}