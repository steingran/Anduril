using System.Text;

namespace Anduril.Communication;

/// <summary>
/// Converts standard Markdown to Teams-compatible Markdown format so that responses
/// from skills and AI providers render correctly in Microsoft Teams.
/// Teams supports a subset of standard Markdown with some differences.
/// </summary>
public static class TeamsMarkdownConverter
{
    /// <summary>
    /// Converts standard Markdown text to Teams-compatible Markdown.
    /// Teams supports most standard Markdown but has some quirks:
    /// - Headers work as-is (# H1, ## H2, etc.)
    /// - Bold: **text** or __text__
    /// - Italic: *text* or _text_
    /// - Strikethrough: ~~text~~
    /// - Code: `code` and ```code blocks```
    /// - Links: [text](url)
    /// - Lists: - item or 1. item
    /// - Blockquotes: > quote
    /// </summary>
    public static string ConvertToTeamsMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        var lines = markdown.Split('\n');
        var result = new StringBuilder(markdown.Length);
        bool inCodeBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                result.Append('\n');

            string line = lines[i].TrimEnd('\r');

            // Track fenced code blocks — don't transform content inside them
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                result.Append(line);
                continue;
            }

            if (inCodeBlock)
            {
                result.Append(line);
                continue;
            }

            line = ConvertLine(line);

            result.Append(line);
        }

        return result.ToString();
    }

    private static string ConvertLine(string line)
    {
        // Teams renders standard Markdown natively: headers, bold, italic,
        // strikethrough, links, lists, blockquotes, and horizontal rules all work as-is.
        return line;
    }
}

