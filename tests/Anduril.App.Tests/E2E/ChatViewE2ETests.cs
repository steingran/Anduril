using Anduril.App.Tests.Infrastructure;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Anduril.App.Tests.E2E;

/// <summary>
/// End-to-end tests that mount the real <see cref="ChatView"/> against a
/// <see cref="ChatViewModel"/> and <see cref="FakeChatService"/>, to verify that the XAML
/// bindings (not just the view model) behave as expected. These tests exercise the full
/// Avalonia control tree — layout, style resolution, and compiled bindings — in the
/// headless platform.
/// </summary>
public sealed class ChatViewE2ETests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task InputTextBox_ReflectsViewModelInputText()
    {
        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-1", new FakeChatService());
            vm.InputText = "hello world";

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();

            var input = FindInput(view);

            await Assert.That(input.Text).IsEqualTo("hello world");

            window.Close();
        });
    }

    [Test]
    public async Task SendButton_IsDisabled_WhenInputTextIsEmpty()
    {
        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-1", new FakeChatService());

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();

            // Flush the ReactiveCommand CanExecute pipeline so the button's IsEnabled
            // reflects the initial empty InputText before we assert.
            Dispatcher.UIThread.RunJobs();

            var sendButton = FindSendButton(view);

            await Assert.That(sendButton.IsEffectivelyEnabled).IsFalse();

            window.Close();
        });
    }

    [Test]
    public async Task SendButton_InvokesChatService_WhenInputHasText()
    {
        var fake = new FakeChatService();

        await RunOnUIThread(async () =>
        {
            var vm = new ChatViewModel();
            vm.SetConversation("conv-1", fake);
            vm.InputText = "Hello agent";

            var view = new ChatView { DataContext = vm };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();

            Dispatcher.UIThread.RunJobs();

            var sendButton = FindSendButton(view);
            await Assert.That(sendButton.IsEffectivelyEnabled).IsTrue();
            await Assert.That(sendButton.Command).IsNotNull();

            sendButton.Command!.Execute(sendButton.CommandParameter);

            // ReactiveCommand.CreateFromTask schedules execution on the main RX scheduler;
            // run pending dispatcher jobs until SendMessageAsync has been observed by the
            // fake service (bounded loop to avoid hanging the test).
            for (var i = 0; i < 20 && fake.SentMessages.Count == 0; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Yield();
            }

            await Assert.That(fake.SentMessages.Count).IsEqualTo(1);
            await Assert.That(fake.SentMessages[0].Text).IsEqualTo("Hello agent");
            await Assert.That(fake.SentMessages[0].ConversationId).IsEqualTo("conv-1");

            window.Close();
        });
    }

    private static TextBox FindInput(ChatView view) =>
        view.GetVisualDescendants()
            .OfType<TextBox>()
            .First(t => t.Classes.Contains("chat-input"));

    private static Button FindSendButton(ChatView view) =>
        view.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Classes.Contains("send"));
}
