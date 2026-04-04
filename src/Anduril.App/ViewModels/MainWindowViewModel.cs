using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Reactive;
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
    private bool _isToolInspectorOpen;

    public MainWindowViewModel(
        IChatService chatService,
        IUserPreferencesService prefsService)
    {
        _chatService = chatService;
        _prefsService = prefsService;
        _prefs = prefsService.Load();
        _currentView = ChatVm;

        NewConversationCommand = ReactiveCommand.CreateFromTask(CreateNewConversationAsync);
        SwitchToChatCommand = ReactiveCommand.Create(() => CurrentView = ChatVm);
        SwitchToCodeCommand = ReactiveCommand.Create(() => CurrentView = CodeVm);
        LoadModelsCommand = ReactiveCommand.CreateFromTask(LoadModelsAsync);
        ToggleToolInspectorCommand = ReactiveCommand.CreateFromTask(ToggleToolInspectorAsync);

        // Load models, then create independent conversations for Chat and Code views
        LoadModelsCommand.Execute().Subscribe(
            unit => { _ = CreateNewConversationAsync(); },
            _ => { });
    }

    public ChatViewModel ChatVm { get; } = new();
    public CodeViewModel CodeVm { get; } = new();

    public ObservableCollection<ModelOption> AvailableModels { get; } = [];
    public ObservableCollection<ConversationEntry> Conversations { get; } = [];
    public ObservableCollection<ToolInspectorItem> ToolInspectorProviders { get; } = [];
    public ObservableCollection<ToolInspectorItem> ToolInspectorIntegrations { get; } = [];

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentView, value);
            this.RaisePropertyChanged(nameof(IsChatActive));
            this.RaisePropertyChanged(nameof(IsCodeActive));
        }
    }

    public bool IsChatActive => _currentView == ChatVm;
    public bool IsCodeActive => _currentView == CodeVm;

    public bool IsToolInspectorOpen
    {
        get => _isToolInspectorOpen;
        set => this.RaiseAndSetIfChanged(ref _isToolInspectorOpen, value);
    }

    public ModelOption? SelectedModel
    {
        get => _selectedModel;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedModel, value);
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

        IsToolInspectorOpen = true;
        await LoadToolsAsync();
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

    private static string FriendlyIntegrationName(string name) => name.ToLowerInvariant() switch
    {
        "" or null         => name,
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

    private static string FriendlyProviderName(string providerName) => providerName.ToLowerInvariant() switch
    {
        "" or null         => providerName,
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
            // Create a conversation for the Chat view
            var conv = await _chatService.CreateConversationAsync();
            var entry = new ConversationEntry { Id = conv.Id, Title = conv.Title ?? "New conversation" };
            Conversations.Insert(0, entry);
            ChatVm.SetConversation(conv.Id, _chatService);
            CurrentView = ChatVm;

            // Create a separate conversation for the Code view
            var codeConv = await _chatService.CreateConversationAsync();
            CodeVm.SetConversation(codeConv.Id, _chatService);
        }
        catch
        {
            // Will retry on next attempt
        }
    }
}

public sealed class ConversationEntry
{
    public required string Id { get; init; }
    public required string Title { get; init; }
}

// DTOs for /api/tools response
file sealed record ToolsResponse(List<ToolProviderDto> Providers, List<ToolIntegrationDto> Integrations);
file sealed record ToolProviderDto(string Name, bool Available, bool SupportsChatCompletion, List<string> Tools);
file sealed record ToolIntegrationDto(string Name, string Description, bool Available, List<string> Functions);
