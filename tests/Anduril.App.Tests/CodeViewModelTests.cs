using System.Reactive.Threading.Tasks;
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
        vm.HasStagedActions = true;

        await vm.AcceptAllCommand.Execute().ToTask();

        await Assert.That(vm.HasStagedActions).IsFalse();
        await Assert.That(vm.StagedActions).IsEmpty();
    }

    [Test]
    public async Task RejectAllCommand_WhenHasStagedActions_ClearsStagedActionsAndSetsHasStagedActionsToFalse()
    {
        var vm = new CodeViewModel();
        vm.HasStagedActions = true;

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
        vm.SetConversation("conv-2", fake);

        await Assert.That(vm.Messages).IsEmpty();
        await Assert.That(vm.StagedActions).IsEmpty();
        await Assert.That(vm.HasStagedActions).IsFalse();
    }

    [Test]
    public async Task AvailableBranches_WhenRepoPathCleared_IsEmpty()
    {
        var vm = new CodeViewModel();
        // Set a path that is not a valid git repo — branches won't load, but
        // clearing the path should empty the collection and null the selection.
        vm.SelectedRepoPath = "/nonexistent/path";
        vm.SelectedRepoPath = null;

        await Assert.That(vm.AvailableBranches).IsEmpty();
        await Assert.That(vm.SelectedBranch).IsNull();
    }
}
