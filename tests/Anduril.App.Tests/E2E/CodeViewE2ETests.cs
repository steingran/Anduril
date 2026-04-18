using Anduril.App.Tests.Infrastructure;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Anduril.App.Tests.E2E;

/// <summary>
/// Phase 2 E2E tests for <see cref="CodeView"/>. These mirror the core ChatView coverage —
/// input binding, send-button gating, and the <c>Ctrl+Return</c> keybinding — so regressions
/// in the Code tab's XAML or <see cref="CodeViewModel"/> are caught by the same harness.
/// </summary>
public sealed class CodeViewE2ETests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task InputTextBox_ReflectsViewModelInputText()
    {
        await RunOnUIThread(async () =>
        {
            var vm = new CodeViewModel();
            vm.SetConversation("conv-code-1", new FakeChatService());
            vm.InputText = "inspect main.py";

            var view = new CodeView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var input = view.FindDescendant<TextBox>(t => t.Classes.Contains("code-input"));
                await Assert.That(input.Text).IsEqualTo("inspect main.py");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task SendButton_IsDisabled_WhenInputTextIsEmpty()
    {
        await RunOnUIThread(async () =>
        {
            var vm = new CodeViewModel();
            vm.SetConversation("conv-code-2", new FakeChatService());

            var view = new CodeView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var sendButton = view.FindDescendant<Button>(b => b.Classes.Contains("send"));
                await Assert.That(sendButton.IsEffectivelyEnabled).IsFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task CtrlReturn_OnCodeInput_InvokesSendCommand()
    {
        var fake = new FakeChatService();

        await RunOnUIThread(async () =>
        {
            var vm = new CodeViewModel();
            vm.SetConversation("conv-code-ctrl", fake);

            var view = new CodeView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var input = view.FindDescendant<TextBox>(t => t.Classes.Contains("code-input"));
                input.Focus();
                Dispatcher.UIThread.RunJobs();

                window.TypeText("review diff");
                Dispatcher.UIThread.RunJobs();
                await Assert.That(vm.InputText).IsEqualTo("review diff");

                window.PressKey(PhysicalKey.Enter, RawInputModifiers.Control);

                var iterations = 0;
                for (; iterations < 20 && fake.SentMessages.Count == 0; iterations++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Yield();
                }

                if (fake.SentMessages.Count == 0)
                {
                    throw new TimeoutException(
                        $"Ctrl+Return did not dispatch CodeView SendCommand after {iterations} dispatcher flushes.");
                }

                await Assert.That(fake.SentMessages.Count).IsEqualTo(1);
                await Assert.That(fake.SentMessages[0].Text).IsEqualTo("review diff");
                await Assert.That(fake.SentMessages[0].ConversationId).IsEqualTo("conv-code-ctrl");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
