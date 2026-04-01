using Anduril.App.Models;

namespace Anduril.App.Tests;

public sealed class CodeMessageModelTests
{
    [Test]
    public async Task IsUser_WhenRoleIsUser_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "user", Content = "hello" };
        await Assert.That(msg.IsUser).IsTrue();
    }

    [Test]
    public async Task IsAssistant_WhenRoleIsAssistant_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = string.Empty };
        await Assert.That(msg.IsAssistant).IsTrue();
    }

    [Test]
    public async Task IsStopped_WhenRoleIsStopped_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "stopped", Content = string.Empty };
        await Assert.That(msg.IsStopped).IsTrue();
    }

    [Test]
    public async Task IsCode_DefaultsToFalse()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = "text" };
        await Assert.That(msg.IsCode).IsFalse();
    }

    [Test]
    public async Task IsCode_WhenSetToTrue_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = "var x = 1;", IsCode = true, Language = "csharp" };
        await Assert.That(msg.IsCode).IsTrue();
        await Assert.That(msg.Language).IsEqualTo("csharp");
    }
}
