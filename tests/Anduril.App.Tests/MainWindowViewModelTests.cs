using Anduril.App.ViewModels;
using Anduril.Core.Communication;

namespace Anduril.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static FakeChatService BuildFakeService(params ProviderInfo[] providers)
    {
        var fake = new FakeChatService { Providers = [.. providers] };
        // Pre-queue conversations for the two CreateConversation calls made during construction
        fake.Conversations.Enqueue(new ConversationInfo { Id = "chat-conv", CreatedAt = DateTimeOffset.UtcNow });
        fake.Conversations.Enqueue(new ConversationInfo { Id = "code-conv", CreatedAt = DateTimeOffset.UtcNow });
        return fake;
    }

    private static ProviderInfo MakeProvider(string id, string name, string model, bool available = true, bool supportsChatCompletion = true) =>
        new() { Id = id, Name = name, Model = model, IsAvailable = available, SupportsChatCompletion = supportsChatCompletion };

    [Test]
    public async Task IsChatActive_Initially_ReturnsTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());

        await Assert.That(vm.IsChatActive).IsTrue();
        await Assert.That(vm.IsCodeActive).IsFalse();
    }

    [Test]
    public async Task SwitchToCodeCommand_MakesIsCodeActiveTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());

        await vm.SwitchToCodeCommand.Execute();

        await Assert.That(vm.IsCodeActive).IsTrue();
        await Assert.That(vm.IsChatActive).IsFalse();
    }

    [Test]
    public async Task SwitchToChatCommand_MakesIsChatActiveTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await vm.SwitchToCodeCommand.Execute();

        await vm.SwitchToChatCommand.Execute();

        await Assert.That(vm.IsChatActive).IsTrue();
    }

    [Test]
    public async Task AvailableModels_AfterLoadModels_ContainsOnlyAvailableProvidersThatSupportChatCompletion()
    {
        var fake = BuildFakeService(
            MakeProvider("p1", "openai", "gpt-4o"),
            MakeProvider("p2", "anthropic", "claude-3"),
            MakeProvider("p3", "ollama", "llama3", available: false),
            MakeProvider("p4", "local", "phi", supportsChatCompletion: false));

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.AvailableModels.Count).IsEqualTo(2);
        await Assert.That(vm.AvailableModels.Select(m => m.ProviderId)).Contains("p1");
        await Assert.That(vm.AvailableModels.Select(m => m.ProviderId)).Contains("p2");
    }

    [Test]
    public async Task SelectedModel_AfterLoadModels_DefaultsToFirstAvailableModel()
    {
        var fake = BuildFakeService(
            MakeProvider("p1", "openai", "gpt-4o"),
            MakeProvider("p2", "anthropic", "claude-3"));

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.SelectedModel).IsNotNull();
        await Assert.That(vm.SelectedModel!.ProviderId).IsEqualTo("p1");
    }

    [Test]
    public async Task SelectedModel_WhenSavedPreferenceExists_RestoresSavedModel()
    {
        var fake = BuildFakeService(
            MakeProvider("p1", "openai", "gpt-4o"),
            MakeProvider("p2", "anthropic", "claude-3"));

        var prefs = new FakeUserPreferencesService();
        prefs.Load().SelectedProviderId = "p2";

        var vm = new MainWindowViewModel(fake, prefs);
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.SelectedModel!.ProviderId).IsEqualTo("p2");
    }

    [Test]
    public async Task SelectedModel_WhenSavedPreferenceDoesNotMatchAnyProvider_FallsBackToFirst()
    {
        var fake = BuildFakeService(
            MakeProvider("p1", "openai", "gpt-4o"));

        var prefs = new FakeUserPreferencesService();
        prefs.Load().SelectedProviderId = "nonexistent-provider";

        var vm = new MainWindowViewModel(fake, prefs);
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.SelectedModel!.ProviderId).IsEqualTo("p1");
    }

    [Test]
    public async Task SelectedModel_WhenSet_CallsSelectModelOnService()
    {
        var fake = BuildFakeService(MakeProvider("p1", "openai", "gpt-4o"));
        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute();

        vm.SelectedModel = vm.AvailableModels[0];

        await Assert.That(fake.SelectedProviderIds).Contains("p1");
    }

    [Test]
    public async Task SelectedModel_WhenSet_SavesPreference()
    {
        var fake = BuildFakeService(MakeProvider("p1", "openai", "gpt-4o"));
        var prefs = new FakeUserPreferencesService();
        var vm = new MainWindowViewModel(fake, prefs);
        await vm.LoadModelsCommand.Execute();

        vm.SelectedModel = vm.AvailableModels[0];
        await Task.Delay(50); // let SaveAsync complete

        await Assert.That(prefs.Load().SelectedProviderId).IsEqualTo("p1");
    }

    [Test]
    public async Task AvailableModels_DisplayName_UsesDisplayNameWhenPresent()
    {
        var fake = BuildFakeService(
            new ProviderInfo { Id = "p1", Name = "anthropic", Model = "claude-3-haiku", DisplayName = "Claude 3 Haiku", IsAvailable = true, SupportsChatCompletion = true });

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.AvailableModels[0].DisplayName).IsEqualTo("Anthropic: Claude 3 Haiku");
    }

    [Test]
    public async Task AvailableModels_DisplayName_FallsBackToModelWhenDisplayNameAbsent()
    {
        var fake = BuildFakeService(
            new ProviderInfo { Id = "p1", Name = "openai", Model = "gpt-4o", DisplayName = null, IsAvailable = true, SupportsChatCompletion = true });

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute();

        await Assert.That(vm.AvailableModels[0].DisplayName).IsEqualTo("OpenAI: gpt-4o");
    }

    [Test]
    public async Task IsToolInspectorOpen_Initially_ReturnsFalse()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await Assert.That(vm.IsToolInspectorOpen).IsFalse();
    }

    [Test]
    public async Task ToggleToolInspectorCommand_WhenClosed_OpensInspector()
    {
        // The LoadToolsAsync call will fail silently (no HTTP server), but IsToolInspectorOpen
        // is set to true before the HTTP call, so this is still testable.
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());

        await vm.ToggleToolInspectorCommand.Execute();

        await Assert.That(vm.IsToolInspectorOpen).IsTrue();
    }

    [Test]
    public async Task ToggleToolInspectorCommand_WhenOpen_ClosesInspector()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await vm.ToggleToolInspectorCommand.Execute();

        await vm.ToggleToolInspectorCommand.Execute();

        await Assert.That(vm.IsToolInspectorOpen).IsFalse();
    }
}
