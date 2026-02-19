using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Skills.Compiled;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Skills.Tests;

public class GmailSkillTests
{
    private static GmailSkill CreateSkill(IEnumerable<IIntegrationTool>? tools = null)
    {
        return new GmailSkill(
            tools ?? [],
            NullLogger<GmailSkill>.Instance);
    }

    private static SkillContext CreateContext(string text) => new()
    {
        Message = new IncomingMessage
        {
            Id = "test-1",
            Text = text,
            UserId = "u1",
            ChannelId = "ch1",
            Platform = "cli"
        },
        UserId = "u1",
        ChannelId = "ch1"
    };

    // ---------------------------------------------------------------
    // Property tests
    // ---------------------------------------------------------------

    [Test]
    public async Task Name_IsGmailEmail()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Name).IsEqualTo("gmail-email");
    }

    [Test]
    public async Task Description_IsNotEmpty()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Description).IsNotEmpty();
    }

    [Test]
    public async Task Triggers_Has18Entries()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers.Count).IsEqualTo(18);
    }

    [Test]
    public async Task Triggers_ContainsCheckMyEmail()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("check my email");
    }

    [Test]
    public async Task Triggers_ContainsOvernightEmails()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("overnight emails");
    }

    [Test]
    public async Task Triggers_ContainsImportantEmail()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("important email");
    }

    [Test]
    public async Task Triggers_ContainsUnrepliedEmail()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("unreplied email");
    }

    [Test]
    public async Task Triggers_ContainsSummarizeEmailThread()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("summarize email thread");
    }

    [Test]
    public async Task Triggers_ContainsEmailPriority()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("email priority");
    }

    // ---------------------------------------------------------------
    // ExecuteAsync routing tests (no tools available)
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_WithOvernightKeyword_ReturnsSuccessWithBriefingHeader()
    {
        var skill = CreateSkill();
        var context = CreateContext("show me overnight emails");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Overnight Email Briefing");
    }

    [Test]
    public async Task ExecuteAsync_WithMorningKeyword_ReturnsOvernightBriefing()
    {
        var skill = CreateSkill();
        var context = CreateContext("morning email briefing please");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Overnight Email Briefing");
    }

    [Test]
    public async Task ExecuteAsync_WithLast24Hours_ReturnsRecentSummary()
    {
        var skill = CreateSkill();
        var context = CreateContext("emails from the last 24 hours");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Email Summary");
    }

    [Test]
    public async Task ExecuteAsync_WithImportantKeyword_ReturnsPriorityHeader()
    {
        var skill = CreateSkill();
        var context = CreateContext("show me important emails");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Important");
    }

    [Test]
    public async Task ExecuteAsync_WithUnrepliedKeyword_ReturnsUnrepliedHeader()
    {
        var skill = CreateSkill();
        var context = CreateContext("show me unreplied emails");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Awaiting Your Reply");
    }

    [Test]
    public async Task ExecuteAsync_WithHaventRespondedKeyword_ReturnsUnrepliedHeader()
    {
        var skill = CreateSkill();
        var context = CreateContext("what email haven't responded to");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Awaiting Your Reply");
    }

    [Test]
    public async Task ExecuteAsync_WithThreadKeyword_ReturnsThreadHint()
    {
        var skill = CreateSkill();
        var context = CreateContext("summarize email thread");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("thread ID");
    }

    [Test]
    public async Task ExecuteAsync_WithLastWeekKeyword_ReturnsWeeklySummary()
    {
        var skill = CreateSkill();
        var context = CreateContext("emails from last week");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Email Summary");
        await Assert.That(result.Response).Contains("Last 7 Days");
    }

    [Test]
    public async Task ExecuteAsync_WithGenericText_ReturnsDefaultSummary()
    {
        var skill = CreateSkill();
        var context = CreateContext("check my email");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("Email Summary");
    }

    [Test]
    public async Task ExecuteAsync_WithNoTools_ReturnsUnavailableMessage()
    {
        var skill = CreateSkill();
        var context = CreateContext("overnight emails");
        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("gmail integration unavailable");
    }
}

