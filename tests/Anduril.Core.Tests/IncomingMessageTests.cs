using Anduril.Core.Communication;

namespace Anduril.Core.Tests;

public class IncomingMessageTests
{
    [Test]
    public async Task CanCreateMinimalMessage()
    {
        var msg = new IncomingMessage
        {
            Id = "msg-1",
            Text = "hello",
            UserId = "user-1",
            ChannelId = "ch-1",
            Platform = "cli"
        };

        await Assert.That(msg.Id).IsEqualTo("msg-1");
        await Assert.That(msg.Text).IsEqualTo("hello");
        await Assert.That(msg.Platform).IsEqualTo("cli");
        await Assert.That(msg.IsDirectMessage).IsFalse();
        await Assert.That(msg.ThreadId).IsNull();
        await Assert.That(msg.UserName).IsNull();
        await Assert.That(msg.Metadata).IsNotNull();
    }

    [Test]
    public async Task DefaultTimestamp_IsRecentUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var msg = new IncomingMessage
        {
            Id = "1",
            Text = "t",
            UserId = "u",
            ChannelId = "c",
            Platform = "cli"
        };
        var after = DateTimeOffset.UtcNow;

        await Assert.That(msg.Timestamp).IsGreaterThanOrEqualTo(before);
        await Assert.That(msg.Timestamp).IsLessThanOrEqualTo(after);
    }
}

