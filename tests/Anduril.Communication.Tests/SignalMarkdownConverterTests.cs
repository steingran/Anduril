using Anduril.Communication;

namespace Anduril.Communication.Tests;

public class SignalMarkdownConverterTests
{
    // --- Null / whitespace ---

    [Test]
    public async Task ConvertToSignalMarkdown_Null_ReturnsNull()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown(null!);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConvertToSignalMarkdown_Empty_ReturnsEmpty()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_Whitespace_ReturnsWhitespace()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("   ");
        await Assert.That(result).IsEqualTo("   ");
    }

    // --- Headers ---

    [Test]
    public async Task ConvertToSignalMarkdown_H1_ConvertsToBold()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("# Main Title");
        await Assert.That(result).IsEqualTo("*Main Title*");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_H2_ConvertsToBold()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("## Section Header");
        await Assert.That(result).IsEqualTo("*Section Header*");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_H3_ConvertsToBold()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("### Sub-section");
        await Assert.That(result).IsEqualTo("*Sub-section*");
    }

    // --- Bold ---

    [Test]
    public async Task ConvertToSignalMarkdown_DoubleAsteriskBold_ConvertsToBold()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("This is **bold** text");
        await Assert.That(result).IsEqualTo("This is *bold* text");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_DoubleUnderscoreBold_ConvertsToBold()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("This is __bold__ text");
        await Assert.That(result).IsEqualTo("This is *bold* text");
    }

    // --- Strikethrough ---

    [Test]
    public async Task ConvertToSignalMarkdown_Strikethrough_ConvertsToSingleTilde()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("This is ~~deleted~~ text");
        await Assert.That(result).IsEqualTo("This is ~deleted~ text");
    }

    // --- Links ---

    [Test]
    public async Task ConvertToSignalMarkdown_Link_ConvertsToTextAndUrl()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("[Click here](https://example.com)");
        await Assert.That(result).IsEqualTo("Click here (https://example.com)");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_MultipleLinks()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("See [A](https://a.com) and [B](https://b.com)");
        await Assert.That(result).IsEqualTo("See A (https://a.com) and B (https://b.com)");
    }

    // --- Bullets ---

    [Test]
    public async Task ConvertToSignalMarkdown_BulletDash_ConvertsToBulletPoint()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("- First item");
        await Assert.That(result).IsEqualTo("• First item");
    }

    [Test]
    public async Task ConvertToSignalMarkdown_NestedBullets_PreservesIndent()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("  - Nested item");
        await Assert.That(result).IsEqualTo("  • Nested item");
    }

    // --- Horizontal rules ---

    [Test]
    public async Task ConvertToSignalMarkdown_TripleDash_ConvertsToDivider()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("---");
        await Assert.That(result).IsEqualTo("───────────────────────────");
    }

    // --- Code blocks (should be preserved) ---

    [Test]
    public async Task ConvertToSignalMarkdown_FencedCodeBlock_PreservesContent()
    {
        string input = "```\n## Not a header\n**not bold**\n- not a bullet\n```";
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown(input);
        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task ConvertToSignalMarkdown_InlineCode_Preserved()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("Use `dotnet build` to compile");
        await Assert.That(result).IsEqualTo("Use `dotnet build` to compile");
    }

    // --- Plain text passthrough ---

    [Test]
    public async Task ConvertToSignalMarkdown_PlainText_PassesThrough()
    {
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown("Just a normal sentence.");
        await Assert.That(result).IsEqualTo("Just a normal sentence.");
    }

    // --- Combined / multi-line ---

    [Test]
    public async Task ConvertToSignalMarkdown_HeaderFollowedByBullets()
    {
        string input = "## Items\n- First\n- Second";
        var result = SignalMarkdownConverter.ConvertToSignalMarkdown(input);
        await Assert.That(result).IsEqualTo("*Items*\n• First\n• Second");
    }
}

