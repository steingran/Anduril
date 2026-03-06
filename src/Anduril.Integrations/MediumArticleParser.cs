using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Anduril.Integrations;

/// <summary>
/// Parses Medium article metadata and content from HTML.
/// </summary>
internal static class MediumArticleParser
{
    private static readonly Regex JsonLdRegex = new(
        "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MetaRegex = new(
        "<meta[^>]+(?<attr>name|property)=[\"'](?<key>[^\"']+)[\"'][^>]+content=[\"'](?<content>[^\"']*)[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        "<title[^>]*>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ArticleRegex = new(
        "<article[^>]*>(?<content>.*?)</article>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static MediumArticleContent Parse(Uri requestUri, string html)
    {
        var jsonLd = ExtractJsonLdMetadata(html, requestUri);
        var canonicalUrl = jsonLd.Url ?? MediumUrlDetector.ExtractCanonicalUrl(html, requestUri) ?? requestUri;
        var title = jsonLd.Title ?? GetMetaContent(html, "og:title") ?? GetMetaContent(html, "twitter:title") ?? ExtractTitle(html) ?? canonicalUrl.AbsolutePath;
        var author = jsonLd.Author ?? GetMetaContent(html, "author") ?? GetMetaContent(html, "twitter:creator");
        var publishedAt = jsonLd.PublishedAt ?? TryParseDate(GetMetaContent(html, "article:published_time"));
        var tags = jsonLd.Tags.Count > 0 ? jsonLd.Tags : GetMetaContents(html, "article:tag");
        var articleHtml = ExtractArticleHtml(html);
        var markdown = HtmlToMarkdown(articleHtml);

        if (string.IsNullOrWhiteSpace(markdown))
            markdown = NormalizePlainText(jsonLd.ArticleBody ?? GetMetaContent(html, "description") ?? string.Empty);

        var plainText = NormalizePlainText(jsonLd.ArticleBody ?? StripHtml(articleHtml));
        var isPaywalled = MediumUrlDetector.IsPaywalled(html);

        if (isPaywalled && string.IsNullOrWhiteSpace(markdown))
            markdown = "_Content preview unavailable because this Medium article appears to be paywalled._";

        return new MediumArticleContent
        {
            SourceUrl = requestUri,
            CanonicalUrl = canonicalUrl,
            Title = WebUtility.HtmlDecode(title).Trim(),
            Author = author is null ? null : WebUtility.HtmlDecode(author).Trim(),
            PublishedAt = publishedAt,
            MarkdownContent = markdown,
            PlainTextContent = plainText,
            Tags = tags,
            IsPaywalled = isPaywalled
        };
    }

    private static (string? Title, string? Author, DateTimeOffset? PublishedAt, List<string> Tags, string? ArticleBody, Uri? Url) ExtractJsonLdMetadata(string html, Uri requestUri)
    {
        string? bestTitle = null;
        string? bestAuthor = null;
        string? bestBody = null;
        Uri? bestUrl = null;
        DateTimeOffset? bestPublishedAt = null;
        List<string> bestTags = [];

        foreach (Match match in JsonLdRegex.Matches(html))
        {
            try
            {
                using var document = JsonDocument.Parse(WebUtility.HtmlDecode(match.Groups["json"].Value));
                Visit(document.RootElement);
            }
            catch (JsonException)
            {
            }
        }

        return (bestTitle, bestAuthor, bestPublishedAt, bestTags, bestBody, bestUrl);

        void Visit(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    ConsiderCandidate(element);
                    foreach (var property in element.EnumerateObject())
                        Visit(property.Value);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        Visit(item);
                    break;
            }
        }

        void ConsiderCandidate(JsonElement candidate)
        {
            var type = GetString(candidate, "@type");
            var title = GetString(candidate, "headline") ?? GetString(candidate, "name");
            var body = GetString(candidate, "articleBody");

            if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                return;

            if (!string.IsNullOrWhiteSpace(type) &&
                !type.Contains("Article", StringComparison.OrdinalIgnoreCase) &&
                !type.Contains("Posting", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(body))
                return;

            if (!string.IsNullOrWhiteSpace(body) && body.Length >= (bestBody?.Length ?? 0))
            {
                bestBody = body;
                bestTitle = title ?? bestTitle;
                bestAuthor = GetNestedName(candidate, "author") ?? bestAuthor;
                bestPublishedAt = TryParseDate(GetString(candidate, "datePublished")) ?? bestPublishedAt;
                bestTags = GetKeywords(candidate);

                var urlValue = GetString(candidate, "url");
                if (Uri.TryCreate(requestUri, urlValue, out var parsedUrl))
                    bestUrl = parsedUrl;
            }
        }
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleRegex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["title"].Value) : null;
    }

    private static string ExtractArticleHtml(string html)
    {
        var match = ArticleRegex.Match(html);
        return match.Success ? match.Groups["content"].Value : string.Empty;
    }

    private static string HtmlToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var markdown = html;
        markdown = Regex.Replace(markdown, "(?is)<pre[^>]*><code[^>]*>(.*?)</code></pre>", match => $"\n```\n{NormalizePlainText(match.Groups[1].Value)}\n```\n");
        markdown = Regex.Replace(markdown, "(?is)<h[1-6][^>]*>(.*?)</h[1-6]>", match => $"\n\n## {NormalizePlainText(match.Groups[1].Value)}\n\n");
        markdown = Regex.Replace(markdown, "(?is)<blockquote[^>]*>(.*?)</blockquote>", match => $"\n\n> {NormalizePlainText(match.Groups[1].Value)}\n\n");
        markdown = Regex.Replace(markdown, "(?is)<li[^>]*>(.*?)</li>", match => $"- {NormalizePlainText(match.Groups[1].Value)}\n");
        markdown = Regex.Replace(markdown, "(?is)<p[^>]*>(.*?)</p>", match => $"\n\n{NormalizePlainText(match.Groups[1].Value)}\n\n");
        markdown = Regex.Replace(markdown, "(?is)<br\\s*/?>", "\n");
        markdown = StripHtml(markdown);
        return NormalizePlainText(markdown);
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(WebUtility.HtmlDecode(value), "(?is)<[^>]+>", string.Empty);
    }

    private static string NormalizePlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = WebUtility.HtmlDecode(value)
            .Replace("\r", string.Empty)
            .Replace("\u00A0", " ");
        normalized = Regex.Replace(normalized, "[ \t]+", " ");
        normalized = Regex.Replace(normalized, " *\n *", "\n");
        normalized = Regex.Replace(normalized, "\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static string? GetMetaContent(string html, string key) =>
        MetaRegex.Matches(html)
            .Select(match => new
            {
                Key = match.Groups["key"].Value,
                Content = match.Groups["content"].Value
            })
            .FirstOrDefault(match => match.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?.Content;

    private static List<string> GetMetaContents(string html, string key) =>
        MetaRegex.Matches(html)
            .Select(match => new
            {
                Key = match.Groups["key"].Value,
                Content = match.Groups["content"].Value
            })
            .Where(match => match.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(match.Content))
            .Select(match => WebUtility.HtmlDecode(match.Content).Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Array => string.Join(", ", property.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString())),
            _ => null
        };
    }

    private static string? GetNestedName(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Object => GetString(property, "name"),
            JsonValueKind.Array => property.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.Object ? GetString(item, "name") : null)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    private static List<string> GetKeywords(JsonElement element)
    {
        if (!element.TryGetProperty("keywords", out var property))
            return [];

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            JsonValueKind.Array => property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => []
        };
    }

    private static DateTimeOffset? TryParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
}