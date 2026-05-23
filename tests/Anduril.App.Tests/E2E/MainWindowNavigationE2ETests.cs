using Anduril.App.Tests.Infrastructure;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

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

                var segmented = FindSegmentedList(window);
                await Assert.That(segmented.SelectedIndex).IsEqualTo(0);

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

                var codeSegment = FindSegmentItem(window, "Code");
                window.ClickCenterOf(codeSegment);

                await HeadlessInputHelpers.FlushUntilAsync(
                    () => vm.IsCodeActive,
                    maxIterations: 20,
                    timeoutMessage: "Clicking the Code tab did not activate CodeVm");

                await Assert.That(vm.IsChatActive).IsFalse();
                await Assert.That(vm.IsCodeActive).IsTrue();

                var segmented = FindSegmentedList(window);
                await Assert.That(segmented.SelectedIndex).IsEqualTo(1);

                var codeView = window.FindDescendant<CodeView>(_ => true);
                await Assert.That(codeView).IsNotNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ClickingSettingsButton_OpensPlaceholderDialog()
    {
        await RunOnUIThread(async () =>
        {
            var (window, vm) = MountMainWindow();
            try
            {
                window.Show();
                await FlushUntilStableAsync();

                var settingsButton = window.FindDescendant<Button>(button => button.Name == "SettingsButton");
                window.ClickCenterOf(settingsButton);

                await HeadlessInputHelpers.FlushUntilAsync(
                    () => vm.IsSettingsOpen,
                    maxIterations: 20,
                    timeoutMessage: "Clicking Settings did not open the placeholder dialog");

                var settingsDialog = window.FindDescendant<Border>(border => border.Name == "SettingsDialog");
                await Assert.That(settingsDialog.IsVisible).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task Escape_ClosesToolInspectorOverlay()
    {
        await RunOnUIThread(async () =>
        {
            var (window, vm) = MountMainWindow();
            try
            {
                window.Show();
                await FlushUntilStableAsync();

                var toolsButton = window.FindDescendant<Button>(button => button.Name == "ToolsToggleButton");
                window.ClickCenterOf(toolsButton);

                await HeadlessInputHelpers.FlushUntilAsync(
                    () => vm.IsToolInspectorOpen,
                    maxIterations: 20,
                    timeoutMessage: "Clicking Tools did not open the inspector");

                window.PressKey(PhysicalKey.Escape);

                await HeadlessInputHelpers.FlushUntilAsync(
                    () => !vm.IsToolInspectorOpen,
                    maxIterations: 20,
                    timeoutMessage: "Escape did not close the tool inspector");

                var inspector = window.FindDescendant<Border>(border => border.Name == "ToolInspectorPanel");
                await Assert.That(inspector.IsHitTestVisible).IsFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ShellNavigationAndSidebarLabels_RenderWithMeasuredText()
    {
        await RunOnUIThread(async () =>
        {
            var (window, _) = MountMainWindow();
            try
            {
                window.Show();
                await FlushUntilStableAsync();

                var chatLabel = window.FindDescendant<TextBlock>(text => text.Text == "Chat");
                var codeLabel = window.FindDescendant<TextBlock>(text => text.Text == "Code");
                var conversationTitle = window.FindDescendant<TextBlock>(text => text.Classes.Contains("conversation-title"));
                var conversationMeta = window.FindDescendant<TextBlock>(text => text.Classes.Contains("conversation-meta"));

                await Assert.That(chatLabel).IsNotNull();
                await Assert.That(codeLabel).IsNotNull();
                await Assert.That(conversationTitle).IsNotNull();
                await Assert.That(conversationMeta).IsNotNull();

                await Assert.That(chatLabel!.Bounds.Width).IsGreaterThan(0d);
                await Assert.That(codeLabel!.Bounds.Width).IsGreaterThan(0d);
                await Assert.That(conversationTitle!.Bounds.Width).IsGreaterThan(0d);
                await Assert.That(conversationMeta!.Bounds.Width).IsGreaterThan(0d);
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

    private static ListBox FindSegmentedList(MainWindow window) =>
        window.FindDescendant<ListBox>(listBox => listBox.Name == "Segments");

    private static ListBoxItem FindSegmentItem(MainWindow window, string content) =>
        window.FindDescendant<ListBoxItem>(item =>
            item.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == content));
}
