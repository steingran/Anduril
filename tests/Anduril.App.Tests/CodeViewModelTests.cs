using System.Reactive.Threading.Tasks;
using Anduril.App.Models;
using Anduril.App.ViewModels;

namespace Anduril.App.Tests;

public sealed class CodeViewModelTests
{
    [Test]
    public async Task HasSelectedRepo_Initially_ReturnsFalse()
    {
        var vm = new CodeViewModel();
        await Assert.That(vm.HasSelectedRepo).IsFalse();
    }

    [Test]
    public async Task RepoDisplayName_Initially_ReturnsEmptyString()
    {
        var vm = new CodeViewModel();
        await Assert.That(vm.RepoDisplayName).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task HasSelectedRepo_AfterSettingRepoPath_ReturnsTrue()
    {
        var vm = new CodeViewModel();
        vm.SelectedRepoPath = "/some/repo/path";
        await Assert.That(vm.HasSelectedRepo).IsTrue();
    }

    [Test]
    public async Task RepoDisplayName_AfterSettingRepoPath_ReturnsLastSegment()
    {
        var vm = new CodeViewModel();
        vm.SelectedRepoPath = "/some/repo/my-project";
        await Assert.That(vm.RepoDisplayName).IsEqualTo("my-project");
    }

    [Test]
    public async Task RepoDisplayName_AfterClearingRepoPath_ReturnsEmptyString()
    {
        var vm = new CodeViewModel();
        vm.SelectedRepoPath = "/some/repo/my-project";
        vm.SelectedRepoPath = null;
        await Assert.That(vm.RepoDisplayName).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task HasStagedActions_Initially_ReturnsFalse()
    {
        var vm = new CodeViewModel();
        await Assert.That(vm.HasStagedActions).IsFalse();
    }

    [Test]
    public async Task AcceptAllCommand_WhenHasStagedActions_ClearsStagedActionsAndSetsHasStagedActionsToFalse()
    {
        var vm = new CodeViewModel();
        vm.StagedActions.Add(new StagedActionModel { Kind = StagedActionKind.Create, FilePath = "test.cs" });

        await vm.AcceptAllCommand.Execute().ToTask();

        await Assert.That(vm.HasStagedActions).IsFalse();
        await Assert.That(vm.StagedActions).IsEmpty();
    }

    [Test]
    public async Task RejectAllCommand_WhenHasStagedActions_ClearsStagedActionsAndSetsHasStagedActionsToFalse()
    {
        var vm = new CodeViewModel();
        vm.StagedActions.Add(new StagedActionModel { Kind = StagedActionKind.Edit, FilePath = "test.cs" });

        await vm.RejectAllCommand.Execute().ToTask();

        await Assert.That(vm.HasStagedActions).IsFalse();
        await Assert.That(vm.StagedActions).IsEmpty();
    }

    [Test]
    public async Task IsStreaming_Initially_ReturnsFalse()
    {
        var vm = new CodeViewModel();
        await Assert.That(vm.IsStreaming).IsFalse();
    }

    [Test]
    public async Task Messages_Initially_IsEmpty()
    {
        var vm = new CodeViewModel();
        await Assert.That(vm.Messages).IsEmpty();
    }

    [Test]
    public async Task SetConversation_ClearsPreviousMessages()
    {
        var vm = new CodeViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);

        // Seed non-empty state so the clearing logic is actually exercised
        vm.Messages.Add(new CodeMessageModel { Role = "user", Content = "hello" });
        vm.StagedActions.Add(new StagedActionModel { Kind = StagedActionKind.Create, FilePath = "test.cs" });

        vm.SetConversation("conv-2", fake);

        await Assert.That(vm.Messages).IsEmpty();
        await Assert.That(vm.StagedActions).IsEmpty();
        await Assert.That(vm.HasStagedActions).IsFalse();
    }

    [Test]
    public async Task AcceptCommand_RemovesOnlyTargetedStagedAction()
    {
        var vm = new CodeViewModel();
        var first = new StagedActionModel { Kind = StagedActionKind.Create, FilePath = "first.cs" };
        var second = new StagedActionModel { Kind = StagedActionKind.Edit, FilePath = "second.cs" };
        vm.StagedActions.Add(first);
        vm.StagedActions.Add(second);

        await vm.AcceptCommand.Execute(first).ToTask();

        await Assert.That(vm.StagedActions.Count).IsEqualTo(1);
        await Assert.That(vm.StagedActions[0].FilePath).IsEqualTo("second.cs");
        await Assert.That(vm.HasStagedActions).IsTrue();
    }

    [Test]
    public async Task RejectCommand_LastItem_ClearsPanelState()
    {
        var vm = new CodeViewModel();
        var only = new StagedActionModel { Kind = StagedActionKind.Delete, FilePath = "obsolete.cs" };
        vm.StagedActions.Add(only);

        await vm.RejectCommand.Execute(only).ToTask();

        await Assert.That(vm.StagedActions).IsEmpty();
        await Assert.That(vm.HasStagedActions).IsFalse();
    }

    [Test]
    public async Task InsertSlashCommand_AppendsShortcutToken()
    {
        var vm = new CodeViewModel
        {
            InputText = "Review auth flow"
        };

        await vm.InsertSlashCommandCommand.Execute("/tests").ToTask();

        await Assert.That(vm.InputText).IsEqualTo("Review auth flow /tests ");
    }

    [Test]
    public async Task AvailableBranches_WhenRepoPathCleared_IsEmpty()
    {
        var vm = new CodeViewModel();
        // Seed non-empty branch state so the clearing logic is genuinely exercised
        // (otherwise this could pass with an always-empty collection).
        vm.AvailableBranches.Add("main");
        vm.SelectedBranch = "main";

        vm.SelectedRepoPath = null;

        await Assert.That(vm.AvailableBranches).IsEmpty();
        await Assert.That(vm.SelectedBranch).IsNull();
    }

    [Test]
    public async Task StagedActionsOverlayLabel_UpdatesWhenCountChangesWithoutClearingAllActions()
    {
        var vm = new CodeViewModel
        {
            ViewportWidth = 900
        };

        var first = new StagedActionModel { Kind = StagedActionKind.Create, FilePath = "first.cs" };
        var second = new StagedActionModel { Kind = StagedActionKind.Edit, FilePath = "second.cs" };
        vm.StagedActions.Add(first);
        vm.StagedActions.Add(second);

        await Assert.That(vm.StagedActionsOverlayLabel).IsEqualTo("Show staged changes (2)");

        await vm.AcceptCommand.Execute(first).ToTask();

        await Assert.That(vm.StagedActionsOverlayLabel).IsEqualTo("Show staged changes (1)");
    }
}
