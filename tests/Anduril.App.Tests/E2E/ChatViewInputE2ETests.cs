using Anduril.App.Tests.Infrastructure;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Anduril.App.Tests.E2E;

/// <summary>
/// Phase 2 E2E tests for <see cref="ChatView"/> that exercise the full input stack: real
/// keyboard events via <c>KeyTextInput</c> / <c>KeyPress</c>, and real pointer events via
/// <c>MouseDown</c> / <c>MouseUp</c> at measured button bounds. Phase 1 tests set
/// <c>vm.InputText</c> directly and invoked <c>Command.Execute</c>; these tests cover the
/// gaps those shortcuts leave — <c>TextBox</c> key handling, the <c>UpdateSourceTrigger</c>
/// binding, the <c>Ctrl+Return</c> send keybinding, and button hit-testing.
/// </summary>
public sealed class ChatViewInputE2ETests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task TypingIntoInputBox_UpdatesViewModelInputText()
    {
        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-1", new FakeChatService());

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var input = view.FindDescendant<TextBox>(t => t.Classes.Contains("chat-input"));
                input.Focus();
                Dispatcher.UIThread.RunJobs();

                window.TypeText("hello");
                Dispatcher.UIThread.RunJobs();

                await Assert.That(vm.InputText).IsEqualTo("hello");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task CtrlReturn_OnInputBox_InvokesSendCommand()
    {
        var fake = new FakeChatService();

        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-ctrl", fake);

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var input = view.FindDescendant<TextBox>(t => t.Classes.Contains("chat-input"));
                input.Focus();
                Dispatcher.UIThread.RunJobs();

                window.TypeText("via keybinding");
                Dispatcher.UIThread.RunJobs();
                await Assert.That(vm.InputText).IsEqualTo("via keybinding");

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
                        $"Ctrl+Return did not dispatch SendCommand after {iterations} dispatcher flushes.");
                }

                await Assert.That(fake.SentMessages.Count).IsEqualTo(1);
                await Assert.That(fake.SentMessages[0].Text).IsEqualTo("via keybinding");
                await Assert.That(fake.SentMessages[0].ConversationId).IsEqualTo("conv-ctrl");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ClickingSendButton_InvokesChatService()
    {
        var fake = new FakeChatService();

        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-click", fake);
            vm.InputText = "clicked send";

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var sendButton = view.FindDescendant<Button>(b => b.Classes.Contains("send"));
                await Assert.That(sendButton.IsEffectivelyEnabled).IsTrue();

                window.ClickCenterOf(sendButton);

                var iterations = 0;
                for (; iterations < 20 && fake.SentMessages.Count == 0; iterations++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Yield();
                }

                if (fake.SentMessages.Count == 0)
                {
                    throw new TimeoutException(
                        $"Send button click did not dispatch SendCommand after {iterations} dispatcher flushes.");
                }

                await Assert.That(fake.SentMessages.Count).IsEqualTo(1);
                await Assert.That(fake.SentMessages[0].Text).IsEqualTo("clicked send");
                await Assert.That(fake.SentMessages[0].ConversationId).IsEqualTo("conv-click");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
