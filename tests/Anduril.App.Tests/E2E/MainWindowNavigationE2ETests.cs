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
                FlushUntilStable();

                await Assert.That(vm.IsChatActive).IsTrue();
                await Assert.That(vm.IsCodeActive).IsFalse();

                var chatTab = FindTabButton(window, "Chat");
                var codeTab = FindTabButton(window, "Code");
                await Assert.That(chatTab.Classes.Contains("active")).IsTrue();
                await Assert.That(codeTab.Classes.Contains("active")).IsFalse();

                _ = window.FindDescendant<ChatView>(_ => true);
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
                FlushUntilStable();

                var codeTab = FindTabButton(window, "Code");
                window.ClickCenterOf(codeTab);

                var iterations = 0;
                for (; iterations < 20 && !vm.IsCodeActive; iterations++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Yield();
                }

                if (!vm.IsCodeActive)
                {
                    throw new TimeoutException(
                        $"Clicking the Code tab did not activate CodeVm after {iterations} dispatcher flushes.");
                }

                await Assert.That(vm.IsChatActive).IsFalse();
                await Assert.That(vm.IsCodeActive).IsTrue();

                var chatTab = FindTabButton(window, "Chat");
                await Assert.That(codeTab.Classes.Contains("active")).IsTrue();
                await Assert.That(chatTab.Classes.Contains("active")).IsFalse();

                _ = window.FindDescendant<CodeView>(_ => true);
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
    /// Pumps dispatcher jobs a few times so layout, the initial <c>LoadModelsCommand</c>
    /// subscription, and the fire-and-forget <c>CreateNewConversationAsync</c> follow-up all
    /// settle before the test asserts visual state.
    /// </summary>
    private static void FlushUntilStable()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Button FindTabButton(MainWindow window, string content) =>
        window.FindDescendant<Button>(b =>
            b.Classes.Contains("tab") && b.Content is string s && s == content);
}
