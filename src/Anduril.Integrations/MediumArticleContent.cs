namespace Anduril.Integrations;

/// <summary>
/// Parsed Medium article content and metadata.
/// </summary>
public sealed record MediumArticleContent
{
    public required Uri SourceUrl { get; init; }
    public required Uri CanonicalUrl { get; init; }
    public required string Title { get; init; }
    public string? Author { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public string MarkdownContent { get; init; } = string.Empty;
    public string PlainTextContent { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool IsPaywalled { get; init; }
}