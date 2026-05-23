using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Anduril.App.Models;
using Anduril.App.Services;
using ReactiveUI;

namespace Anduril.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HttpClient s_httpClient = new();

    private readonly IChatService _chatService;
    private readonly IUserPreferencesService _prefsService;
    private readonly UserPreferences _prefs;
    private ViewModelBase _currentView;
    private ModelOption? _selectedModel;
    private ConversationEntry? _selectedConversation;
    private bool _isToolInspectorOpen;
    private bool _isSettingsOpen;
    private int _selectedNavigationIndex;
    private bool _isSwitchingConversation;

    public MainWindowViewModel(
        IChatService chatService,
        IUserPreferencesService prefsService)
    {
        _chatService = chatService;
        _prefsService = prefsService;
        _prefs = prefsService.Load();
        _currentView = ChatVm;

        NavigationItems =
        [
            "Chat",
            "Code"
        ];

        NewConversationCommand = ReactiveCommand.CreateFromTask(CreateNewConversationAsync);
        SwitchToChatCommand = ReactiveCommand.Create(() =>
        {
            SelectedNavigationIndex = 0;
            return CurrentView;
        });
        SwitchToCodeCommand = ReactiveCommand.Create(() =>
        {
            SelectedNavigationIndex = 1;
            return CurrentView;
        });
        LoadModelsCommand = ReactiveCommand.CreateFromTask(LoadModelsAsync);
        ToggleToolInspectorCommand = ReactiveCommand.CreateFromTask(ToggleToolInspectorAsync);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        CloseSettingsCommand = ReactiveCommand.Create(CloseSettings);
        CloseTransientPanelsCommand = ReactiveCommand.Create(CloseTransientPanels);
        RenameConversationCommand = ReactiveCommand.Create<ConversationEntry>(RenameConversation);
        DeleteConversationCommand = ReactiveCommand.Create<ConversationEntry>(DeleteConversation);
        ChatVm.AvailableModels = AvailableModels;
        ChatVm.ConfigureProviderCommand = OpenSettingsCommand;
        ChatVm.WhenAnyValue(vm => vm.SelectedModel)
            .Where(model => model is not null)
            .Subscribe(model =>
            {
                if (!Equals(_selectedModel, model))
                    SelectedModel = model;
            });
        this.WhenAnyValue(vm => vm.SelectedConversation)
            .Where(conversation => conversation is not null)
            .Subscribe(conversation => _ = SwitchConversationAsync(conversation!));

        // Load models, then create independent conversations for Chat and Code views
        LoadModelsCommand.Execute().Subscribe(
            unit => { _ = CreateNewConversationAsync(); },
            _ => { });
    }

    public ChatViewModel ChatVm { get; } = new();
    public CodeViewModel CodeVm { get; } = new();

    public ObservableCollection<ModelOption> AvailableModels { get; } = [];
    public ObservableCollection<string> NavigationItems { get; }
    public ObservableCollection<ConversationEntry> Conversations { get; } = [];
    public ObservableCollection<ConversationGroup> GroupedConversations { get; } = [];
    public ObservableCollection<ToolInspectorItem> ToolInspectorProviders { get; } = [];
    public ObservableCollection<ToolInspectorItem> ToolInspectorIntegrations { get; } = [];

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentView, value);
            var desiredIndex = value == CodeVm ? 1 : 0;
            if (_selectedNavigationIndex != desiredIndex)
            {
                _selectedNavigationIndex = desiredIndex;
                this.RaisePropertyChanged(nameof(SelectedNavigationIndex));
            }

            this.RaisePropertyChanged(nameof(IsChatActive));
            this.RaisePropertyChanged(nameof(IsCodeActive));
        }
    }

    public bool IsChatActive => _currentView == ChatVm;
    public bool IsCodeActive => _currentView == CodeVm;

    public bool IsToolInspectorOpen
    {
        get => _isToolInspectorOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isToolInspectorOpen, value);
            this.RaisePropertyChanged(nameof(IsOverlayOpen));
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
            this.RaisePropertyChanged(nameof(IsOverlayOpen));
        }
    }

    public bool IsOverlayOpen => IsToolInspectorOpen || IsSettingsOpen;

    public ConversationEntry? SelectedConversation
    {
        get => _selectedConversation;
        set => this.RaiseAndSetIfChanged(ref _selectedConversation, value);
    }

    public int SelectedNavigationIndex
    {
        get => _selectedNavigationIndex;
        set
        {
            if (_selectedNavigationIndex == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedNavigationIndex, value);
            CurrentView = value == 1 ? CodeVm : ChatVm;
        }
    }

    public ModelOption? SelectedModel
    {
        get => _selectedModel;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedModel, value);
            if (!Equals(ChatVm.SelectedModel, value))
                ChatVm.SelectedModel = value;
            if (value is not null)
            {
                _ = _chatService.SelectModelAsync(value.ProviderId).ContinueWith(
                    t => { if (t.IsFaulted) System.Diagnostics.Debug.WriteLine(t.Exception); },
                    TaskScheduler.Default);
                _prefs.SelectedProviderId = value.ProviderId;
                _ = _prefsService.SaveAsync(_prefs).ContinueWith(
                    t => { if (t.IsFaulted) System.Diagnostics.Debug.WriteLine(t.Exception); },
                    TaskScheduler.Default);
            }
        }
    }

    public ReactiveCommand<Unit, Unit> NewConversationCommand { get; }
    public ReactiveCommand<Unit, ViewModelBase> SwitchToChatCommand { get; }
    public ReactiveCommand<Unit, ViewModelBase> SwitchToCodeCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadModelsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleToolInspectorCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseTransientPanelsCommand { get; }
    public ReactiveCommand<ConversationEntry, Unit> RenameConversationCommand { get; }
    public ReactiveCommand<ConversationEntry, Unit> DeleteConversationCommand { get; }

    private async Task LoadModelsAsync()
    {
        try
        {
            var providers = await _chatService.GetAvailableProvidersAsync();
            AvailableModels.Clear();
            foreach (var p in providers.Where(p => p.IsAvailable && p.SupportsChatCompletion))
            {
                var modelLabel = !string.IsNullOrWhiteSpace(p.DisplayName)
                    ? p.DisplayName
                    : p.Model;
                AvailableModels.Add(new ModelOption
                {
                    ProviderId = p.Id,
                    DisplayName = $"{FriendlyProviderName(p.Name)}: {modelLabel}",
                    ModelName = p.Model,
                    IsAvailable = p.IsAvailable
                });
            }

            if (AvailableModels.Count > 0 && SelectedModel is null)
            {
                // Restore the previously selected model, or fall back to the first available.
                var savedModel = _prefs.SelectedProviderId is not null
                    ? AvailableModels.FirstOrDefault(m => m.ProviderId == _prefs.SelectedProviderId)
                    : null;
                SelectedModel = savedModel ?? AvailableModels[0];
            }
        }
        catch
        {
            // Models will be loaded when connection is established
        }
    }

    private async Task ToggleToolInspectorAsync()
    {
        if (IsToolInspectorOpen)
        {
            IsToolInspectorOpen = false;
            return;
        }

        IsSettingsOpen = false;
        IsToolInspectorOpen = true;
        await LoadToolsAsync();
    }

    private void OpenSettings()
    {
        IsToolInspectorOpen = false;
        IsSettingsOpen = true;
    }

    private void CloseSettings() => IsSettingsOpen = false;

    private void CloseTransientPanels()
    {
        IsToolInspectorOpen = false;
        IsSettingsOpen = false;
    }

    private async Task LoadToolsAsync()
    {
        try
        {
            var response = await s_httpClient.GetFromJsonAsync<ToolsResponse>(
                $"{HostService.BaseUrl}/api/tools", _jsonOptions);

            if (response is null) return;

            ToolInspectorProviders.Clear();
            foreach (var p in response.Providers)
            {
                ToolInspectorProviders.Add(new ToolInspectorItem
                {
                    Name = FriendlyProviderName(p.Name),
                    IsAvailable = p.Available,
                    SupportsChatCompletion = p.SupportsChatCompletion,
                    Functions = p.Tools
                });
            }

            ToolInspectorIntegrations.Clear();
            foreach (var t in response.Integrations)
            {
                ToolInspectorIntegrations.Add(new ToolInspectorItem
                {
                    Name = FriendlyIntegrationName(t.Name),
                    Description = t.Description,
                    IsAvailable = t.Available,
                    Functions = t.Functions
                });
            }
        }
        catch
        {
            // Tool inspector is a convenience feature — silently ignore failures
        }
    }

    private static string FriendlyIntegrationName(string name) => name?.ToLowerInvariant() switch
    {
        null or ""         => name ?? string.Empty,
        "github"           => "GitHub",
        "sentry"           => "Sentry",
        "office365calendar"
            or "office365-calendar" => "Office 365 Calendar",
        "gmail"            => "Gmail",
        "protonmail"       => "Proton Mail",
        "slackquery"       => "Slack",
        "mediumarticle"    => "Medium Articles",
        "weeklymenu"       => "Weekly Menu Planner",
        "code"             => "Code Repository",
        { Length: 1 } single => char.ToUpper(single[0]).ToString(),
        var other          => char.ToUpper(other[0]) + other[1..]
    };

    private static string FriendlyProviderName(string providerName) => providerName?.ToLowerInvariant() switch
    {
        null or ""         => providerName ?? string.Empty,
        "anthropic"        => "Anthropic",
        "copilot"          => "Copilot",
        "openai"           => "OpenAI",
        "ollama"           => "Local",
        "llamasharp"       => "Local",
        "augment"          => "Augment",
        "augment-chat"     => "Augment",
        { Length: 1 } single => char.ToUpper(single[0]).ToString(),
        var other          => char.ToUpper(other[0]) + other[1..]
    };

    private async Task CreateNewConversationAsync()
    {
        try
        {
            // Create both conversations before mutating UI state, so a failure on the second
            // call doesn't leave the Chat view initialised while the Code view is not.
            var conv = await _chatService.CreateConversationAsync();
            var codeConv = await _chatService.CreateConversationAsync();

            var entry = new ConversationEntry
            {
                ChatConversationId = conv.Id,
                CodeConversationId = codeConv.Id,
                Title = string.IsNullOrWhiteSpace(conv.Title) ? "New conversation" : conv.Title,
                CreatedAt = conv.CreatedAt
            };
            Conversations.Insert(0, entry);
            SelectedConversation = entry;
            RebuildConversationGroups();
            SelectedNavigationIndex = 0;
        }
        catch
        {
            // Will retry on next attempt
        }
    }

    private async Task SwitchConversationAsync(ConversationEntry conversation)
    {
        if (_isSwitchingConversation)
            return;

        _isSwitchingConversation = true;
        try
        {
            var chatHistoryTask = _chatService.GetConversationHistoryAsync(conversation.ChatConversationId);
            var codeHistoryTask = _chatService.GetConversationHistoryAsync(conversation.CodeConversationId);
            await Task.WhenAll(chatHistoryTask, codeHistoryTask);

            ChatVm.SetConversation(conversation.ChatConversationId, _chatService, chatHistoryTask.Result);
            CodeVm.SetConversation(conversation.CodeConversationId, _chatService, codeHistoryTask.Result);
        }
        catch
        {
            // Keep the current conversation if history loading fails.
        }
        finally
        {
            _isSwitchingConversation = false;
        }
    }

    private void RenameConversation(ConversationEntry conversation)
    {
        if (conversation.Title.StartsWith("Renamed:", StringComparison.Ordinal))
            return;

        conversation.Title = $"Renamed: {conversation.Title}";
        RebuildConversationGroups();
    }

    private void DeleteConversation(ConversationEntry conversation)
    {
        if (!Conversations.Remove(conversation))
            return;

        if (ReferenceEquals(SelectedConversation, conversation))
            SelectedConversation = Conversations.FirstOrDefault();

        RebuildConversationGroups();
    }

    private void RebuildConversationGroups()
    {
        var today = DateTimeOffset.Now.Date;
        var grouped = Conversations
            .GroupBy(conversation => GetGroupLabel(conversation.CreatedAt, today))
            .OrderBy(group => GetGroupOrder(group.Key))
            .Select(group => new ConversationGroup(
                group.Key,
                new ObservableCollection<ConversationEntry>(
                    group.OrderByDescending(conversation => conversation.CreatedAt))));

        GroupedConversations.Clear();
        foreach (var group in grouped)
            GroupedConversations.Add(group);
    }

    private static string GetGroupLabel(DateTimeOffset createdAt, DateTime today)
    {
        var createdDate = createdAt.LocalDateTime.Date;
        if (createdDate == today)
            return "Today";

        if (createdDate == today.AddDays(-1))
            return "Yesterday";

        if (createdDate >= today.AddDays(-7))
            return "Last 7 days";

        return "Earlier";
    }

    private static int GetGroupOrder(string label) => label switch
    {
        "Today" => 0,
        "Yesterday" => 1,
        "Last 7 days" => 2,
        _ => 3
    };
}

public sealed class ConversationEntry : ReactiveObject
{
    private string _title = string.Empty;

    public string Id => ChatConversationId;
    public required string ChatConversationId { get; init; }
    public required string CodeConversationId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public required string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }
}

public sealed record ConversationGroup(string Header, ObservableCollection<ConversationEntry> Conversations);

// DTOs for /api/tools response
file sealed record ToolsResponse(List<ToolProviderDto> Providers, List<ToolIntegrationDto> Integrations);
file sealed record ToolProviderDto(string Name, bool Available, bool SupportsChatCompletion, List<string> Tools);
file sealed record ToolIntegrationDto(string Name, string Description, bool Available, List<string> Functions);
