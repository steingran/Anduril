using Anduril.Communication;

namespace Anduril.Communication.Tests;

public class TeamsMarkdownConverterTests
{
    // --- Null / empty / whitespace ---

    [Test]
    public async Task ConvertToTeamsMarkdown_NullInput_ReturnsNull()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(null!);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_WhitespaceOnly_ReturnsWhitespace()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("   ");
        await Assert.That(result).IsEqualTo("   ");
    }

    // --- Plain text passthrough ---

    [Test]
    public async Task ConvertToTeamsMarkdown_PlainText_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("Just a normal sentence.");
        await Assert.That(result).IsEqualTo("Just a normal sentence.");
    }

    // --- Native Markdown passthrough (Teams renders these natively) ---

    [Test]
    public async Task ConvertToTeamsMarkdown_H1_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("# Main Title");
        await Assert.That(result).IsEqualTo("# Main Title");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_H2_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("## Section Header");
        await Assert.That(result).IsEqualTo("## Section Header");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_Bold_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("This is **bold** text");
        await Assert.That(result).IsEqualTo("This is **bold** text");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_Italic_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("This is *italic* text");
        await Assert.That(result).IsEqualTo("This is *italic* text");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_Strikethrough_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("This is ~~deleted~~ text");
        await Assert.That(result).IsEqualTo("This is ~~deleted~~ text");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_Link_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("[Click here](https://example.com)");
        await Assert.That(result).IsEqualTo("[Click here](https://example.com)");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_BulletList_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("- First item");
        await Assert.That(result).IsEqualTo("- First item");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_BlockQuote_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("> This is a quote");
        await Assert.That(result).IsEqualTo("> This is a quote");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_HorizontalRule_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("---");
        await Assert.That(result).IsEqualTo("---");
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_InlineCode_PassesThrough()
    {
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown("Use `dotnet build` to compile");
        await Assert.That(result).IsEqualTo("Use `dotnet build` to compile");
    }

    // --- Fenced code block handling ---

    [Test]
    public async Task ConvertToTeamsMarkdown_FencedCodeBlock_PreservesContentVerbatim()
    {
        string input = "```\n## Not a header\n**not converted**\n- not a bullet\n```";
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_FencedCodeBlockWithLanguage_PreservesContent()
    {
        string input = "```csharp\nvar x = 1;\n```";
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_TextBeforeAndAfterCodeBlock_PassesThrough()
    {
        string input = "Before\n```\n## inside code\n```\nAfter **bold**";
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    // --- Multi-line passthrough ---

    [Test]
    public async Task ConvertToTeamsMarkdown_HeaderFollowedByBullets_PassesThrough()
    {
        string input = "## Items\n- First\n- Second";
        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertToTeamsMarkdown_RealisticSkillOutput_PassesThrough()
    {
        string input =
            "## Tech & AI News\n" +
            "- **OpenAI**: Released new model\n" +
            "\n" +
            "---\n" +
            "**Action Items**: You have an overdue VAT filing.";

        var result = TeamsMarkdownConverter.ConvertToTeamsMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }
}

