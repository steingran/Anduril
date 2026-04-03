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

    [Test]
    public async Task IsLoading_WhenAssistantWithEmptyContent_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = string.Empty };
        await Assert.That(msg.IsLoading).IsTrue();
    }

    [Test]
    public async Task IsLoading_WhenAssistantWithContent_ReturnsFalse()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = "some code" };
        await Assert.That(msg.IsLoading).IsFalse();
    }

    [Test]
    public async Task IsLoading_WhenUserRole_ReturnsFalse()
    {
        var msg = new CodeMessageModel { Role = "user", Content = string.Empty };
        await Assert.That(msg.IsLoading).IsFalse();
    }

    [Test]
    public async Task IsCollapsible_WhenContentIsShort_ReturnsFalse()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = "short" };
        await Assert.That(msg.IsCollapsible).IsFalse();
    }

    [Test]
    public async Task IsCollapsible_WhenContentExceedsThreshold_ReturnsTrue()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.IsCollapsible).IsTrue();
    }

    [Test]
    public async Task ContentMaxHeight_WhenCollapsibleAndNotExpanded_Returns200()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.ContentMaxHeight).IsEqualTo(200.0);
    }

    [Test]
    public async Task ContentMaxHeight_WhenExpanded_ReturnsInfinity()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.IsExpanded = true;
        await Assert.That(msg.ContentMaxHeight).IsEqualTo(double.PositiveInfinity);
    }

    [Test]
    public async Task ShowMoreLabel_Initially_ReturnsShowMore()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.ShowMoreLabel).IsEqualTo("Show more");
    }

    [Test]
    public async Task ShowMoreLabel_WhenExpanded_ReturnsShowLess()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.IsExpanded = true;
        await Assert.That(msg.ShowMoreLabel).IsEqualTo("Show less");
    }

    [Test]
    public async Task ToggleExpandCommand_WhenExecuted_FlipsIsExpanded()
    {
        var msg = new CodeMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.ToggleExpandCommand.Execute(null);
        await Assert.That(msg.IsExpanded).IsTrue();
    }
}
