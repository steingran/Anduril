using System.Text;
using System.Text.RegularExpressions;

namespace Anduril.Communication;

/// <summary>
/// Converts standard Markdown to Signal-compatible formatting so that responses
/// from skills and AI providers render correctly in Signal clients.
/// Signal supports: *bold*, _italic_, ~strikethrough~, `code`, and ```code blocks```.
/// </summary>
public static partial class SignalMarkdownConverter
{
    /// <summary>
    /// Converts standard Markdown text to Signal-compatible formatting.
    /// </summary>
    public static string ConvertToSignalMarkdown(string markdown)
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
        string trimmed = line.TrimStart();

        // Horizontal rules: --- or *** or ___ → Unicode line
        if (HorizontalRuleRegex().IsMatch(trimmed))
            return "───────────────────────────";

        // Headers: ### H3, ## H2, # H1 → *bold text* (Signal has no native header support)
        if (trimmed.StartsWith("### "))
            return $"*{trimmed[4..].Trim()}*";
        if (trimmed.StartsWith("## "))
            return $"*{trimmed[3..].Trim()}*";
        if (trimmed.StartsWith("# "))
            return $"*{trimmed[2..].Trim()}*";

        // Bullet points: - item → • item (preserve leading whitespace for nesting)
        if (BulletRegex().IsMatch(line))
        {
            int dashIndex = line.IndexOf('-');
            string indent = line[..dashIndex];
            string content = line[(dashIndex + 1)..].TrimStart();
            line = $"{indent}• {content}";
        }

        // Inline conversions
        line = ConvertInlineFormatting(line);

        return line;
    }

    private static string ConvertInlineFormatting(string line)
    {
        // Links: [text](url) → text (url) — Signal does not support hyperlink formatting
        line = LinkRegex().Replace(line, "$1 ($2)");

        // Bold: **text** or __text__ → *text* (Signal uses single asterisks for bold)
        line = BoldDoubleAsteriskRegex().Replace(line, "*$1*");
        line = BoldDoubleUnderscoreRegex().Replace(line, "*$1*");

        // Strikethrough: ~~text~~ → ~text~ (Signal uses single tildes)
        line = StrikethroughRegex().Replace(line, "~$1~");

        return line;
    }

    // --- Generated regex patterns ---

    [GeneratedRegex(@"^[-*_]{3,}\s*$")]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^(\s*)-\s")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldDoubleAsteriskRegex();

    [GeneratedRegex(@"__(.+?)__")]
    private static partial Regex BoldDoubleUnderscoreRegex();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();
}

