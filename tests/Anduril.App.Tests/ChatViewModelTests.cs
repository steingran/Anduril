using System.Reactive.Threading.Tasks;
using Anduril.App.ViewModels;

namespace Anduril.App.Tests;

public sealed class ChatViewModelTests
{
    [Test]
    public async Task IsStreaming_Initially_ReturnsFalse()
    {
        var vm = new ChatViewModel();
        await Assert.That(vm.IsStreaming).IsFalse();
    }

    [Test]
    public async Task InputText_Initially_IsEmpty()
    {
        var vm = new ChatViewModel();
        await Assert.That(vm.InputText).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Messages_Initially_IsEmpty()
    {
        var vm = new ChatViewModel();
        await Assert.That(vm.Messages).IsEmpty();
    }

    [Test]
    public async Task SetConversation_ClearsPreviousMessages()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.SetConversation("conv-2", fake);

        await Assert.That(vm.Messages).IsEmpty();
    }

    [Test]
    public async Task SendCommand_WhenInputIsEmpty_CannotExecute()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = string.Empty;

        bool canExecute = false;
        vm.SendCommand.CanExecute.Subscribe(x => canExecute = x);

        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task SendCommand_WhenInputIsWhitespace_CannotExecute()
    {
        var vm = new ChatViewModel();
        vm.InputText = "   ";

        bool canExecute = false;
        vm.SendCommand.CanExecute.Subscribe(x => canExecute = x);

        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task SendCommand_WhenInputHasText_CanExecute()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = "Hello";

        bool canExecute = false;
        vm.SendCommand.CanExecute.Subscribe(x => canExecute = x);

        await Assert.That(canExecute).IsTrue();
    }

    [Test]
    public async Task SendCommand_WhenExecuted_AddsUserAndAssistantMessages()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = "Hello";

        await vm.SendCommand.Execute().ToTask();

        await Assert.That(vm.Messages.Count).IsEqualTo(2);
        await Assert.That(vm.Messages[0].IsUser).IsTrue();
        await Assert.That(vm.Messages[0].Content).IsEqualTo("Hello");
        await Assert.That(vm.Messages[1].IsAssistant).IsTrue();
    }

    [Test]
    public async Task SendCommand_WhenExecuted_ClearsInputText()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = "Hello";

        await vm.SendCommand.Execute().ToTask();

        await Assert.That(vm.InputText).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SendCommand_WhenExecuted_SendsMessageToService()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = "Hello agent";

        await vm.SendCommand.Execute().ToTask();

        await Assert.That(fake.SentMessages.Count).IsEqualTo(1);
        await Assert.That(fake.SentMessages[0].Text).IsEqualTo("Hello agent");
        await Assert.That(fake.SentMessages[0].ConversationId).IsEqualTo("conv-1");
    }

    [Test]
    public async Task SendCommand_WhenExecuted_SetsIsStreamingTrue()
    {
        var vm = new ChatViewModel();
        var fake = new FakeChatService();
        vm.SetConversation("conv-1", fake);
        vm.InputText = "Hello";

        await vm.SendCommand.Execute().ToTask();

        await Assert.That(vm.IsStreaming).IsTrue();
    }
}
