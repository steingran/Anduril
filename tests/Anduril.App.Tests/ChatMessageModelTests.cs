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

    [Test]
    public async Task IsCollapsible_WhenContentIsShort_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "short" };
        await Assert.That(msg.IsCollapsible).IsFalse();
    }

    [Test]
    public async Task IsCollapsible_WhenContentExceedsThreshold_ReturnsTrue()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.IsCollapsible).IsTrue();
    }

    [Test]
    public async Task ContentMaxHeight_WhenCollapsibleAndNotExpanded_Returns320()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.ContentMaxHeight).IsEqualTo(320.0);
    }

    [Test]
    public async Task ContentMaxHeight_WhenExpanded_ReturnsInfinity()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.IsExpanded = true;
        await Assert.That(msg.ContentMaxHeight).IsEqualTo(double.PositiveInfinity);
    }

    [Test]
    public async Task ContentMaxHeight_WhenNotCollapsible_ReturnsInfinity()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "short" };
        await Assert.That(msg.ContentMaxHeight).IsEqualTo(double.PositiveInfinity);
    }

    [Test]
    public async Task ShowMoreLabel_Initially_ReturnsShowMore()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.ShowMoreLabel).IsEqualTo("Show more");
    }

    [Test]
    public async Task ShowMoreLabel_WhenExpanded_ReturnsShowLess()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.IsExpanded = true;
        await Assert.That(msg.ShowMoreLabel).IsEqualTo("Show less");
    }

    [Test]
    public async Task ToggleExpandCommand_WhenExecuted_FlipsIsExpanded()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        await Assert.That(msg.IsExpanded).IsFalse();

        msg.ToggleExpandCommand.Execute(null);

        await Assert.That(msg.IsExpanded).IsTrue();
    }

    [Test]
    public async Task ToggleExpandCommand_WhenExecutedTwice_ReturnsFalse()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = new string('x', 501) };
        msg.ToggleExpandCommand.Execute(null);
        msg.ToggleExpandCommand.Execute(null);
        await Assert.That(msg.IsExpanded).IsFalse();
    }

    [Test]
    public async Task ToolCalls_WhenSet_ReportsHasToolCalls()
    {
        var msg = new ChatMessageModel
        {
            Role = "assistant",
            Content = "done",
            ToolCalls =
            [
                new ToolCallSummary
                {
                    ToolId = "search",
                    ToolName = "search",
                    Detail = "inspected repo"
                }
            ]
        };

        await Assert.That(msg.HasToolCalls).IsTrue();
        await Assert.That(msg.ToolCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ContentBlocks_WhenContentContainsCodeFence_ParsesCodeBlock()
    {
        var msg = new ChatMessageModel
        {
            Role = "assistant",
            Content = "Intro\n```csharp\nConsole.WriteLine(\"hi\");\n```\nDone"
        };

        await Assert.That(msg.ContentBlocks.Count).IsEqualTo(3);
        await Assert.That(msg.ContentBlocks[1]).IsTypeOf<CodeChatContentBlock>();
    }

    [Test]
    public async Task TokenCountLabel_UsesEstimatedTokenCount()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = "12345678" };
        await Assert.That(msg.TokenCountLabel).IsEqualTo("2 tok");
    }

    [Test]
    public async Task EstimatedTokenCount_WhenContentIsEmpty_ReturnsZero()
    {
        var msg = new ChatMessageModel { Role = "assistant", Content = string.Empty };
        await Assert.That(msg.EstimatedTokenCount).IsEqualTo(0);
        await Assert.That(msg.TokenCountLabel).IsEqualTo("0 tok");
    }
}
