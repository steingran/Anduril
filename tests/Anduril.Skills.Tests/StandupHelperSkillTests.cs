using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Skills.Compiled;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anduril.Skills.Tests;

public class StandupHelperSkillTests
{
    private static StandupHelperSkill CreateSkill(IEnumerable<IIntegrationTool>? tools = null)
    {
        return new StandupHelperSkill(
            tools ?? [],
            NullLogger<StandupHelperSkill>.Instance);
    }

    // ---------------------------------------------------------------
    // Property tests
    // ---------------------------------------------------------------

    [Test]
    public async Task Name_IsStandupHelper()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Name).IsEqualTo("standup-helper");
    }

    [Test]
    public async Task Description_IsNotEmpty()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Description).IsNotEmpty();
    }

    [Test]
    public async Task Triggers_ContainsStandup()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("standup");
    }

    [Test]
    public async Task Triggers_ContainsGenerateStandup()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers).Contains("generate standup");
    }

    [Test]
    public async Task Triggers_HasFiveEntries()
    {
        var skill = CreateSkill();
        await Assert.That(skill.Triggers.Count).IsEqualTo(5);
    }

    // ---------------------------------------------------------------
    // CalculateLastStandupTime tests
    // ---------------------------------------------------------------

    [Test]
    public async Task CalculateLastStandupTime_OnWednesdayAfternoon_ReturnsMondayMorning()
    {
        // Wednesday 2026-02-18 14:00 → last standup was Monday 2026-02-16 09:25
        var now = new DateTime(2026, 2, 18, 14, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 16, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_OnMondayAfternoon_ReturnsLastWednesday()
    {
        // Monday 2026-02-16 14:00 → last standup was Wednesday 2026-02-11 09:25
        var now = new DateTime(2026, 2, 16, 14, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 11, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_OnThursdayMorning_ReturnsWednesday()
    {
        // Thursday 2026-02-19 08:00 → last standup was Wednesday 2026-02-18 09:25
        var now = new DateTime(2026, 2, 19, 8, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 18, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_OnFriday_ReturnsWednesday()
    {
        // Friday 2026-02-20 10:00 → last standup was Wednesday 2026-02-18 09:25
        var now = new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 18, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_OnSunday_ReturnsLastWednesday()
    {
        // Sunday 2026-02-22 12:00 → last standup was Wednesday 2026-02-18 09:25
        var now = new DateTime(2026, 2, 22, 12, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 18, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_OnTuesday_ReturnsMonday()
    {
        // Tuesday 2026-02-17 15:00 → last standup was Monday 2026-02-16 09:25
        var now = new DateTime(2026, 2, 17, 15, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result).IsEqualTo(new DateTime(2026, 2, 16, 9, 25, 0));
    }

    [Test]
    public async Task CalculateLastStandupTime_ReturnsTimeAt0925()
    {
        var now = new DateTime(2026, 2, 18, 14, 0, 0, DateTimeKind.Utc);
        var result = StandupHelperSkill.CalculateLastStandupTime(now);

        await Assert.That(result.Hour).IsEqualTo(9);
        await Assert.That(result.Minute).IsEqualTo(25);
        await Assert.That(result.Second).IsEqualTo(0);
    }

    // ---------------------------------------------------------------
    // ExecuteAsync tests
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_WithNoTools_ReturnsSuccessWithUnavailableMessages()
    {
        var skill = CreateSkill();
        var context = new SkillContext
        {
            Message = new IncomingMessage
            {
                Id = "test-1", Text = "standup", UserId = "u1",
                ChannelId = "ch1", Platform = "cli"
            },
            UserId = "u1",
            ChannelId = "ch1"
        };

        var result = await skill.ExecuteAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).Contains("github integration unavailable");
        await Assert.That(result.Response).Contains("office365-calendar integration unavailable");
    }
}

