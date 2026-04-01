using Anduril.App.Models;

namespace Anduril.App.Tests;

public sealed class ChatMessageModelTests
{
    [Test]
    public async Task IsUser_WhenRoleIsUser_ReturnsTrue()
    {
        var msg = new ChatMessageModel { Role = "user", Content = "hello" };
        await Assert.That(msg.IsUser).IsTrue();
    }

    [Test]
    public async Task IsUser_WhenRoleIsAssistant_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "hi" };
        await Assert.That(msg.IsUser).IsFalse();
    }

    [Test]
    public async Task IsAssistant_WhenRoleIsAssistant_ReturnsTrue()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "hi" };
        await Assert.That(msg.IsAssistant).IsTrue();
    }

    [Test]
    public async Task IsAssistant_WhenRoleIsUser_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "user", Content = "hello" };
        await Assert.That(msg.IsAssistant).IsFalse();
    }

    [Test]
    public async Task IsStopped_WhenRoleIsStopped_ReturnsTrue()
    {
        var msg = new ChatMessageModel { Role = "stopped", Content = string.Empty };
        await Assert.That(msg.IsStopped).IsTrue();
    }

    [Test]
    public async Task IsStopped_WhenRoleIsUser_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "user", Content = "hello" };
        await Assert.That(msg.IsStopped).IsFalse();
    }

    [Test]
    public async Task IsLoading_WhenAssistantWithEmptyContent_ReturnsTrue()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = string.Empty };
        await Assert.That(msg.IsLoading).IsTrue();
    }

    [Test]
    public async Task IsLoading_WhenAssistantWithContent_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "some text" };
        await Assert.That(msg.IsLoading).IsFalse();
    }

    [Test]
    public async Task IsLoading_WhenUserRole_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "user", Content = string.Empty };
        await Assert.That(msg.IsLoading).IsFalse();
    }

    [Test]
    public async Task Timestamp_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var msg = new ChatMessageModel { Role = "user", Content = "hello" };
        var after = DateTimeOffset.UtcNow;

        await Assert.That(msg.Timestamp).IsGreaterThanOrEqualTo(before);
        await Assert.That(msg.Timestamp).IsLessThanOrEqualTo(after);
    }
}
