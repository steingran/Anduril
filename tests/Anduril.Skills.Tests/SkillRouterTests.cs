using Anduril.Core.Communication;
using Anduril.Core.Skills;
using Anduril.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Skills.Tests;

public class SkillRouterTests
{
    private readonly SkillRouter _router = new(NullLogger<SkillRouter>.Instance);

    [Test]
    public async Task RouteAsync_EmptyText_ReturnsNull()
    {
        var msg = CreateMessage("");
        var result = await _router.RouteAsync(msg);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RouteAsync_NoSkillsRegistered_ReturnsNull()
    {
        var msg = CreateMessage("review my code");
        var result = await _router.RouteAsync(msg);

        await Assert.That(result).IsNull();
    }

    // ---------------------------------------------------------------
    // Hybrid routing — ratio threshold tests
    // ---------------------------------------------------------------

    [Test]
    public async Task RouteAsync_SpecificCommand_RoutesToSkill()
    {
        var router = await CreateRouterWithSkill("standup-helper", ["standup", "daily standup"]);

        var result = await router.RouteAsync(CreateMessage("standup"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("standup-helper");
    }

    [Test]
    public async Task RouteAsync_ShortCommandWithMinimalPadding_RoutesToSkill()
    {
        var router = await CreateRouterWithSkill("standup-helper", ["standup", "daily standup"]);

        // "run standup" → trigger "standup" (7) / message length (11) = 64% > 30%
        var result = await router.RouteAsync(CreateMessage("run standup"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("standup-helper");
    }

    [Test]
    public async Task RouteAsync_VagueConversationalMessage_ReturnsNull()
    {
        var router = await CreateRouterWithSkill("gmail-email",
            ["check my email", "email summary", "overnight emails"]);

        // Only a tiny trigger match relative to the full message length
        // None of the triggers appear as substrings in this message
        var result = await router.RouteAsync(
            CreateMessage("can you please help me figure out what to do with my inbox today"));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RouteAsync_LongMessageWithWeakMatch_ReturnsNull()
    {
        var router = await CreateRouterWithSkill("gmail-email",
            ["email from", "email summary", "read email"]);

        // "email" appears but as part of a long sentence — low ratio
        // "read email" (10) / 68 chars = 15% < 30%
        var result = await router.RouteAsync(
            CreateMessage("I'm wondering if you could read email messages and tell me what's important"));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RouteAsync_DirectTriggerPhrase_RoutesToSkill()
    {
        var router = await CreateRouterWithSkill("gmail-email",
            ["check my email", "overnight emails", "morning email briefing"]);

        // Exact trigger match: "check my email" (14) / 14 = 100%
        var result = await router.RouteAsync(CreateMessage("check my email"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("gmail-email");
    }

    [Test]
    public async Task RouteAsync_TriggerWithSmallSuffix_RoutesToSkill()
    {
        var router = await CreateRouterWithSkill("gmail-email",
            ["check my email", "overnight emails"]);

        // "check my email" (14) / "check my email please" (21) = 67% > 30%
        var result = await router.RouteAsync(CreateMessage("check my email please"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("gmail-email");
    }

    [Test]
    public async Task RouteAsync_MultipleSkills_RoutesToBestMatch()
    {
        var router = new SkillRouter(NullLogger<SkillRouter>.Instance);
        router.RegisterRunner(new FakeSkillRunner("compiled",
        [
            new SkillInfo
            {
                Name = "standup-helper",
                Description = "Standup",
                SkillType = "compiled",
                Triggers = ["standup", "daily standup"]
            },
            new SkillInfo
            {
                Name = "gmail-email",
                Description = "Email",
                SkillType = "compiled",
                Triggers = ["check my email", "email summary"]
            }
        ]));
        await router.RefreshAsync();

        // "daily standup" matches standup-helper more strongly
        var result = await router.RouteAsync(CreateMessage("daily standup"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("standup-helper");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static async Task<SkillRouter> CreateRouterWithSkill(
        string skillName, string[] triggers)
    {
        var router = new SkillRouter(NullLogger<SkillRouter>.Instance);
        router.RegisterRunner(new FakeSkillRunner("compiled",
        [
            new SkillInfo
            {
                Name = skillName,
                Description = "Test skill",
                SkillType = "compiled",
                Triggers = triggers
            }
        ]));
        await router.RefreshAsync();
        return router;
    }

    private static IncomingMessage CreateMessage(string text) => new()
    {
        Id = "test-1",
        Text = text,
        UserId = "user-1",
        ChannelId = "ch-1",
        Platform = "cli"
    };

    /// <summary>
    /// Minimal ISkillRunner that returns pre-built SkillInfo entries.
    /// </summary>
    private sealed class FakeSkillRunner(string skillType, List<SkillInfo> skills) : ISkillRunner
    {
        public string SkillType => skillType;

        public Task<IReadOnlyList<SkillInfo>> GetSkillsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SkillInfo>>(skills);

        public bool CanHandle(SkillInfo skill) => skill.SkillType == skillType;

        public Task<SkillResult> ExecuteAsync(string skillName, SkillContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SkillResult.Ok("fake"));
    }
}

