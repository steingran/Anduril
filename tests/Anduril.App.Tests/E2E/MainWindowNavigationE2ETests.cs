using Anduril.App.Tests.Infrastructure;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Anduril.App.Tests.E2E;

/// <summary>
/// Phase 2 multi-view flow test: mounts the real <see cref="MainWindow"/> with a
/// <see cref="MainWindowViewModel"/> wired to fakes, asserts the Chat tab is active by
/// default, then clicks the Code tab via real pointer events and confirms the
/// <c>ContentControl</c> swapped in a <see cref="CodeView"/>.
/// </summary>
public sealed class MainWindowNavigationE2ETests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task ChatTab_IsActive_ByDefault()
    {
        await RunOnUIThread(async () =>
        {
            var (window, vm) = MountMainWindow();
            try
            {
                window.Show();
                await FlushUntilStableAsync();

                await Assert.That(vm.IsChatActive).IsTrue();
                await Assert.That(vm.IsCodeActive).IsFalse();

                var chatTab = FindTabButton(window, "Chat");
                var codeTab = FindTabButton(window, "Code");
                await Assert.That(chatTab.Classes.Contains("active")).IsTrue();
                await Assert.That(codeTab.Classes.Contains("active")).IsFalse();

                var chatView = window.FindDescendant<ChatView>(_ => true);
                await Assert.That(chatView).IsNotNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ClickingCodeTab_SwapsActiveView_ToCodeView()
    {
        await RunOnUIThread(async () =>
        {
            var (window, vm) = MountMainWindow();
            try
            {
                window.Show();
                await FlushUntilStableAsync();

                var codeTab = FindTabButton(window, "Code");
                window.ClickCenterOf(codeTab);

                await HeadlessInputHelpers.FlushUntilAsync(
                    () => vm.IsCodeActive,
                    maxIterations: 20,
                    timeoutMessage: "Clicking the Code tab did not activate CodeVm");

                await Assert.That(vm.IsChatActive).IsFalse();
                await Assert.That(vm.IsCodeActive).IsTrue();

                var chatTab = FindTabButton(window, "Chat");
                await Assert.That(codeTab.Classes.Contains("active")).IsTrue();
                await Assert.That(chatTab.Classes.Contains("active")).IsFalse();

                var codeView = window.FindDescendant<CodeView>(_ => true);
                await Assert.That(codeView).IsNotNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static (MainWindow Window, MainWindowViewModel Vm) MountMainWindow()
    {
        var vm = new MainWindowViewModel(new FakeChatService(), new FakeUserPreferencesService());
        var window = new MainWindow { DataContext = vm };
        return (window, vm);
    }

    /// <summary>
    /// Pumps dispatcher jobs with yields between iterations so layout, the initial
    /// <c>LoadModelsCommand</c> subscription, and the fire-and-forget
    /// <c>CreateNewConversationAsync</c> follow-up on <see cref="MainWindowViewModel"/> all
    /// settle before the test asserts visual state. Synchronous <c>RunJobs</c> calls alone
    /// don't give Task-returning continuations a chance to run — the <c>await Task.Yield()</c>
    /// is what lets the async chain make progress.
    /// </summary>
    private static async Task FlushUntilStableAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    private static Button FindTabButton(MainWindow window, string content) =>
        window.FindDescendant<Button>(b =>
            b.Classes.Contains("tab") && b.Content is string s && s == content);
}
