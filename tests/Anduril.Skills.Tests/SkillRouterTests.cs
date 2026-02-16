using Anduril.Core.Communication;
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

    private static IncomingMessage CreateMessage(string text) => new()
    {
        Id = "test-1",
        Text = text,
        UserId = "user-1",
        ChannelId = "ch-1",
        Platform = "cli"
    };
}

