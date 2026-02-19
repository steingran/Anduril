using Anduril.Communication;

namespace Anduril.Communication.Tests;

public class SlackMarkdownConverterTests
{
    // --- Null / empty / whitespace ---

    [Test]
    public async Task ConvertToSlackMarkdown_NullInput_ReturnsNull()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown(null!);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConvertToSlackMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_WhitespaceOnly_ReturnsWhitespace()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("   ");
        await Assert.That(result).IsEqualTo("   ");
    }

    // --- Headers ---

    [Test]
    public async Task ConvertToSlackMarkdown_H1_ConvertsToBold()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("# Main Title");
        await Assert.That(result).IsEqualTo("*Main Title*");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_H2_ConvertsToBold()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("## Section Header");
        await Assert.That(result).IsEqualTo("*Section Header*");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_H3_ConvertsToBold()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("### Sub-section");
        await Assert.That(result).IsEqualTo("*Sub-section*");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_HeaderWithEmoji_PreservesEmoji()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("### 📧 Overnight Email Briefing");
        await Assert.That(result).IsEqualTo("*📧 Overnight Email Briefing*");
    }

    // --- Bold ---

    [Test]
    public async Task ConvertToSlackMarkdown_DoubleAsteriskBold_ConvertsToBold()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("This is **bold** text");
        await Assert.That(result).IsEqualTo("This is *bold* text");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_DoubleUnderscoreBold_ConvertsToBold()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("This is __bold__ text");
        await Assert.That(result).IsEqualTo("This is *bold* text");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_MultipleBoldsOnOneLine()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("**First** and **Second**");
        await Assert.That(result).IsEqualTo("*First* and *Second*");
    }

    // --- Strikethrough ---

    [Test]
    public async Task ConvertToSlackMarkdown_Strikethrough_ConvertsToSingleTilde()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("This is ~~deleted~~ text");
        await Assert.That(result).IsEqualTo("This is ~deleted~ text");
    }

    // --- Links ---

    [Test]
    public async Task ConvertToSlackMarkdown_Link_ConvertsToSlackFormat()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("[Click here](https://example.com)");
        await Assert.That(result).IsEqualTo("<https://example.com|Click here>");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_MultipleLinks()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("See [A](https://a.com) and [B](https://b.com)");
        await Assert.That(result).IsEqualTo("See <https://a.com|A> and <https://b.com|B>");
    }

    // --- Bullets ---

    [Test]
    public async Task ConvertToSlackMarkdown_BulletDash_ConvertsToBulletPoint()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("- First item");
        await Assert.That(result).IsEqualTo("• First item");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_NestedBullets_PreservesIndent()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("  - Nested item");
        await Assert.That(result).IsEqualTo("  • Nested item");
    }

    // --- Horizontal rules ---

    [Test]
    public async Task ConvertToSlackMarkdown_TripleDash_ConvertsToDivider()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("---");
        await Assert.That(result).IsEqualTo("───────────────────────────");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_TripleAsterisk_ConvertsToDivider()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("***");
        await Assert.That(result).IsEqualTo("───────────────────────────");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_TripleUnderscore_ConvertsToDivider()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("___");
        await Assert.That(result).IsEqualTo("───────────────────────────");
    }

    // --- Code blocks (should be preserved) ---

    [Test]
    public async Task ConvertToSlackMarkdown_FencedCodeBlock_PreservesContent()
    {
        string input = "```\n## Not a header\n**not bold**\n- not a bullet\n```";
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertToSlackMarkdown_InlineCode_Preserved()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("Use `dotnet build` to compile");
        await Assert.That(result).IsEqualTo("Use `dotnet build` to compile");
    }

    // --- Plain text passthrough ---

    [Test]
    public async Task ConvertToSlackMarkdown_PlainText_PassesThrough()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("Just a normal sentence.");
        await Assert.That(result).IsEqualTo("Just a normal sentence.");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_BlockQuote_PassesThrough()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("> This is a quote");
        await Assert.That(result).IsEqualTo("> This is a quote");
    }

    // --- Combined / multi-line ---

    [Test]
    public async Task ConvertToSlackMarkdown_BulletWithBoldContent()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("- **OpenAI**: Released new model");
        await Assert.That(result).IsEqualTo("• *OpenAI*: Released new model");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_HeaderFollowedByBullets()
    {
        string input = "## Items\n- First\n- Second";
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown(input);
        await Assert.That(result).IsEqualTo("*Items*\n• First\n• Second");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_TextBeforeAndAfterCodeBlock()
    {
        string input = "Before\n```\n## inside code\n```\nAfter **bold**";
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown(input);
        await Assert.That(result).IsEqualTo("Before\n```\n## inside code\n```\nAfter *bold*");
    }

    [Test]
    public async Task ConvertToSlackMarkdown_BulletWithLink()
    {
        var result = SlackMarkdownConverter.ConvertToSlackMarkdown("- See [docs](https://docs.example.com)");
        await Assert.That(result).IsEqualTo("• See <https://docs.example.com|docs>");
    }

    // --- Realistic email summary (matches the user's example output) ---

    [Test]
    public async Task ConvertToSlackMarkdown_RealisticEmailSummary()
    {
        string input =
            "## Tech & AI News\n" +
            "- **OpenAI**: Released new model\n" +
            "- **ByteByteGo**: Technical deep-dives\n" +
            "\n" +
            "## Personal\n" +
            "- **Fiken**: Tax reminders\n" +
            "\n" +
            "---\n" +
            "**Action Items**: You have an overdue VAT filing.";

        string expected =
            "*Tech & AI News*\n" +
            "• *OpenAI*: Released new model\n" +
            "• *ByteByteGo*: Technical deep-dives\n" +
            "\n" +
            "*Personal*\n" +
            "• *Fiken*: Tax reminders\n" +
            "\n" +
            "───────────────────────────\n" +
            "*Action Items*: You have an overdue VAT filing.";

        var result = SlackMarkdownConverter.ConvertToSlackMarkdown(input);
        await Assert.That(result).IsEqualTo(expected);
    }
}

