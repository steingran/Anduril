using System.Reactive.Threading.Tasks;
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

    private static async Task WaitForConversationCountAsync(MainWindowViewModel vm, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (vm.Conversations.Count < expectedCount && DateTime.UtcNow < deadline)
            await Task.Yield();
    }

    [Test]
    public async Task IsChatActive_Initially_ReturnsTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);

        await Assert.That(vm.IsChatActive).IsTrue();
        await Assert.That(vm.IsCodeActive).IsFalse();
        await Assert.That(vm.SelectedNavigationIndex).IsEqualTo(0);
    }

    [Test]
    public async Task SwitchToCodeCommand_MakesIsCodeActiveTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);

        await vm.SwitchToCodeCommand.Execute().ToTask();

        await Assert.That(vm.IsCodeActive).IsTrue();
        await Assert.That(vm.IsChatActive).IsFalse();
        await Assert.That(vm.SelectedNavigationIndex).IsEqualTo(1);
    }

    [Test]
    public async Task SwitchToChatCommand_MakesIsChatActiveTrue()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);
        await vm.SwitchToCodeCommand.Execute().ToTask();

        await vm.SwitchToChatCommand.Execute().ToTask();

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
        await vm.LoadModelsCommand.Execute().ToTask();

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
        await vm.LoadModelsCommand.Execute().ToTask();

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
        await vm.LoadModelsCommand.Execute().ToTask();

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
        await vm.LoadModelsCommand.Execute().ToTask();

        await Assert.That(vm.SelectedModel!.ProviderId).IsEqualTo("p1");
    }

    [Test]
    public async Task SelectedModel_WhenSet_CallsSelectModelOnService()
    {
        var fake = BuildFakeService(MakeProvider("p1", "openai", "gpt-4o"));
        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute().ToTask();

        // Clear any recorded calls from initial model auto-selection.
        fake.SelectedProviderIds.Clear();

        vm.SelectedModel = vm.AvailableModels[0];

        // Poll until the fire-and-forget SelectModelAsync completes (scheduled on thread pool).
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!fake.SelectedProviderIds.Contains("p1") && DateTime.UtcNow < deadline)
            await Task.Yield();

        await Assert.That(fake.SelectedProviderIds).Contains("p1");
    }

    [Test]
    public async Task SelectedModel_WhenSet_SavesPreference()
    {
        var fake = BuildFakeService(MakeProvider("p1", "openai", "gpt-4o"));
        var prefs = new FakeUserPreferencesService();
        var vm = new MainWindowViewModel(fake, prefs);
        await vm.LoadModelsCommand.Execute().ToTask();

        vm.SelectedModel = vm.AvailableModels[0];

        // Poll until the fire-and-forget SaveAsync completes (scheduled on thread pool).
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (prefs.Load().SelectedProviderId != "p1" && DateTime.UtcNow < deadline)
            await Task.Yield();

        await Assert.That(prefs.Load().SelectedProviderId).IsEqualTo("p1");
    }

    [Test]
    public async Task AvailableModels_DisplayName_UsesDisplayNameWhenPresent()
    {
        var fake = BuildFakeService(
            new ProviderInfo { Id = "p1", Name = "anthropic", Model = "claude-3-haiku", DisplayName = "Claude 3 Haiku", IsAvailable = true, SupportsChatCompletion = true });

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute().ToTask();

        await Assert.That(vm.AvailableModels[0].DisplayName).IsEqualTo("Anthropic: Claude 3 Haiku");
    }

    [Test]
    public async Task AvailableModels_DisplayName_FallsBackToModelWhenDisplayNameAbsent()
    {
        var fake = BuildFakeService(
            new ProviderInfo { Id = "p1", Name = "openai", Model = "gpt-4o", DisplayName = null, IsAvailable = true, SupportsChatCompletion = true });

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await vm.LoadModelsCommand.Execute().ToTask();

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
        await WaitForConversationCountAsync(vm, 1);

        await vm.ToggleToolInspectorCommand.Execute().ToTask();

        await Assert.That(vm.IsToolInspectorOpen).IsTrue();
        await Assert.That(vm.IsOverlayOpen).IsTrue();
    }

    [Test]
    public async Task ToggleToolInspectorCommand_WhenOpen_ClosesInspector()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);
        await vm.ToggleToolInspectorCommand.Execute().ToTask();

        await vm.ToggleToolInspectorCommand.Execute().ToTask();

        await Assert.That(vm.IsToolInspectorOpen).IsFalse();
        await Assert.That(vm.IsOverlayOpen).IsFalse();
    }

    [Test]
    public async Task OpenSettingsCommand_OpensSettings_AndClosesToolInspector()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);
        await vm.ToggleToolInspectorCommand.Execute().ToTask();

        await vm.OpenSettingsCommand.Execute().ToTask();

        await Assert.That(vm.IsSettingsOpen).IsTrue();
        await Assert.That(vm.IsToolInspectorOpen).IsFalse();
        await Assert.That(vm.IsOverlayOpen).IsTrue();
    }

    [Test]
    public async Task CloseTransientPanelsCommand_ClosesSettingsAndInspector()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);
        await vm.OpenSettingsCommand.Execute().ToTask();

        await vm.CloseTransientPanelsCommand.Execute().ToTask();

        await Assert.That(vm.IsSettingsOpen).IsFalse();
        await Assert.That(vm.IsToolInspectorOpen).IsFalse();
        await Assert.That(vm.IsOverlayOpen).IsFalse();
    }

    [Test]
    public async Task GroupedConversations_GroupsEntriesByRelativeDate()
    {
        var now = DateTimeOffset.Now;
        var fake = new FakeChatService
        {
            Conversations = new Queue<ConversationInfo>(
            [
                new ConversationInfo { Id = "today-chat", Title = "Today thread", CreatedAt = now },
                new ConversationInfo { Id = "today-code", CreatedAt = now },
                new ConversationInfo { Id = "yesterday-chat", Title = "Yesterday thread", CreatedAt = now.AddDays(-1) },
                new ConversationInfo { Id = "yesterday-code", CreatedAt = now.AddDays(-1) },
                new ConversationInfo { Id = "week-chat", Title = "Week thread", CreatedAt = now.AddDays(-3) },
                new ConversationInfo { Id = "week-code", CreatedAt = now.AddDays(-3) },
                new ConversationInfo { Id = "earlier-chat", Title = "Earlier thread", CreatedAt = now.AddDays(-20) },
                new ConversationInfo { Id = "earlier-code", CreatedAt = now.AddDays(-20) }
            ])
        };

        var vm = new MainWindowViewModel(fake, new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);
        await vm.NewConversationCommand.Execute().ToTask();
        await vm.NewConversationCommand.Execute().ToTask();
        await vm.NewConversationCommand.Execute().ToTask();

        await Assert.That(vm.GroupedConversations.Count).IsEqualTo(4);
        await Assert.That(vm.GroupedConversations[0].Header).IsEqualTo("Today");
        await Assert.That(vm.GroupedConversations[1].Header).IsEqualTo("Yesterday");
        await Assert.That(vm.GroupedConversations[2].Header).IsEqualTo("Last 7 days");
        await Assert.That(vm.GroupedConversations[3].Header).IsEqualTo("Earlier");
        await Assert.That(vm.GroupedConversations[1].Conversations[0].Title).IsEqualTo("Yesterday thread");
    }

    [Test]
    public async Task RenameAndDeleteConversationCommands_UpdateConversationCollections()
    {
        var vm = new MainWindowViewModel(BuildFakeService(), new FakeUserPreferencesService());
        await WaitForConversationCountAsync(vm, 1);

        var conversation = vm.Conversations[0];

        await vm.RenameConversationCommand.Execute(conversation).ToTask();

        await Assert.That(conversation.Title).StartsWith("Renamed:");

        await vm.DeleteConversationCommand.Execute(conversation).ToTask();

        await Assert.That(vm.Conversations.Count).IsEqualTo(0);
        await Assert.That(vm.GroupedConversations.Count).IsEqualTo(0);
    }
}
