using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Anduril.App.Models;
using Anduril.App.Services;
using Anduril.Core.Communication;
using Avalonia.Threading;
using ReactiveUI;

namespace Anduril.App.ViewModels;

public sealed class ChatViewModel : ViewModelBase
{
    private IChatService? _chatService;
    private string? _conversationId;
    private string _inputText = string.Empty;
    private bool _isStreaming;
    private ChatErrorState? _lastError;
    private string? _lastCopiedText;
    private SlashCommandSuggestion? _selectedSlashCommandSuggestion;
    private IEnumerable<ModelOption>? _availableModels;
    private ModelOption? _selectedModel;
    private bool _isApplyingSlashCommand;
    private ChatMessageModel? _latestAssistantMessage;

    public ChatViewModel()
    {
        SendCommand = ReactiveCommand.CreateFromTask(
            SendMessageAsync,
            this.WhenAnyValue(x => x.InputText, x => x.IsStreaming,
                (text, streaming) => !string.IsNullOrWhiteSpace(text) && !streaming));

        StopCommand = ReactiveCommand.CreateFromTask(
            StopStreamingAsync,
            this.WhenAnyValue(x => x.IsStreaming).Select(streaming => streaming));

        RegenerateCommand = ReactiveCommand.CreateFromTask(
            RegenerateLastAssistantAsync,
            this.WhenAnyValue(x => x.CanRegenerate).Select(canRegenerate => canRegenerate));

        CopyCommand = ReactiveCommand.Create<string?>(CopyMessage);
        AttachCommand = ReactiveCommand.Create(AttachContext, this.WhenAnyValue(x => x.CanAttach).Select(canAttach => canAttach));
        RetryCommand = ReactiveCommand.CreateFromTask(
            RetryLastMessageAsync,
            this.WhenAnyValue(x => x.LastError).Select(error => error is not null));

        ConfigureProviderCommand = ReactiveCommand.Create(() => { });
        UseStarterPromptCommand = ReactiveCommand.Create<StarterPrompt?>(UseStarterPrompt);
        ApplySlashCommandCommand = ReactiveCommand.Create<SlashCommandSuggestion?>(ApplySlashCommand);

        StarterPrompts =
        [
            new StarterPrompt
            {
                Title = "Summarize the repo",
                Prompt = "Summarize the current repository and highlight the riskiest areas to change next.",
                Description = "Quick architecture scan"
            },
            new StarterPrompt
            {
                Title = "Plan a refactor",
                Prompt = "Propose a focused refactor plan for the current feature area with risks and test coverage gaps.",
                Description = "Design + sequencing"
            },
            new StarterPrompt
            {
                Title = "Review recent work",
                Prompt = "Review the current changes for regressions, missing tests, and unclear design edges.",
                Description = "Code review mode"
            }
        ];

        SlashCommandSuggestions =
        [
            new SlashCommandSuggestion
            {
                Command = "/explain",
                Title = "Explain current file",
                Description = "Ask Anduril to explain the current code or output."
            },
            new SlashCommandSuggestion
            {
                Command = "/review",
                Title = "Review the diff",
                Description = "Request a bug-focused review of the active changes."
            },
            new SlashCommandSuggestion
            {
                Command = "/tests",
                Title = "Generate tests",
                Description = "Ask for targeted test coverage for the current change."
            },
            new SlashCommandSuggestion
            {
                Command = "/plan",
                Title = "Draft a plan",
                Description = "Break a larger request into concrete steps before implementation."
            }
        ];
    }

    public ObservableCollection<ChatMessageModel> Messages { get; } = [];
    public ObservableCollection<StarterPrompt> StarterPrompts { get; }
    public ObservableCollection<SlashCommandSuggestion> SlashCommandSuggestions { get; }
    public ObservableCollection<SlashCommandSuggestion> VisibleSlashCommandSuggestions { get; } = [];

    public string InputText
    {
        get => _inputText;
        set
        {
            this.RaiseAndSetIfChanged(ref _inputText, value);
            UpdateSlashSuggestions();
        }
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            this.RaiseAndSetIfChanged(ref _isStreaming, value);
            this.RaisePropertyChanged(nameof(CanRegenerate));
            UpdateMessageStates();
        }
    }

    public ChatErrorState? LastError
    {
        get => _lastError;
        set
        {
            this.RaiseAndSetIfChanged(ref _lastError, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public string? LastCopiedText
    {
        get => _lastCopiedText;
        private set => this.RaiseAndSetIfChanged(ref _lastCopiedText, value);
    }

    public SlashCommandSuggestion? SelectedSlashCommandSuggestion
    {
        get => _selectedSlashCommandSuggestion;
        set => this.RaiseAndSetIfChanged(ref _selectedSlashCommandSuggestion, value);
    }

    public IEnumerable<ModelOption>? AvailableModels
    {
        get => _availableModels;
        set => this.RaiseAndSetIfChanged(ref _availableModels, value);
    }

    public ModelOption? SelectedModel
    {
        get => _selectedModel;
        set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
    }

    public bool HasMessages => Messages.Count > 0;
    public bool HasError => LastError is not null;
    public bool CanAttach => true;
    public bool CanRegenerate => !IsStreaming && Messages.Any(message => message.IsAssistant) && Messages.Any(message => message.IsUser);
    public bool IsSlashCommandMenuOpen =>
        !_isApplyingSlashCommand &&
        InputText.StartsWith("/", StringComparison.Ordinal) &&
        !ContainsSlashTermTerminator(InputText.AsSpan(1)) &&
        VisibleSlashCommandSuggestions.Count > 0;

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> RegenerateCommand { get; }
    public ReactiveCommand<string?, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachCommand { get; }
    public ReactiveCommand<Unit, Unit> RetryCommand { get; }
    public ICommand? ConfigureProviderCommand { get; set; }
    public ReactiveCommand<StarterPrompt?, Unit> UseStarterPromptCommand { get; }
    public ReactiveCommand<SlashCommandSuggestion?, Unit> ApplySlashCommandCommand { get; }

    public void SetConversation(string conversationId, IChatService chatService, IReadOnlyList<SessionMessage>? history = null)
    {
        // Unsubscribe from previous
        if (_chatService is not null)
        {
            _chatService.TokenReceived -= OnTokenReceived;
        }

        _conversationId = conversationId;
        _chatService = chatService;
        _chatService.TokenReceived += OnTokenReceived;

        // If a response was mid-stream when this conversation was replaced, reset the streaming
        // state immediately. The in-flight IsComplete token from the hub will be ignored because
        // the conversationId no longer matches.
        if (IsStreaming)
            IsStreaming = false;

        Messages.Clear();
        foreach (var message in history ?? [])
        {
            Messages.Add(new ChatMessageModel
            {
                Role = message.Role,
                Content = message.Content,
                Timestamp = message.Timestamp
            });
        }
        LastError = null;
        this.RaisePropertyChanged(nameof(HasMessages));
        this.RaisePropertyChanged(nameof(CanRegenerate));
        UpdateMessageStates();
    }

    private async Task StopStreamingAsync()
    {
        if (_chatService is null || _conversationId is null) return;
        await _chatService.CancelMessageAsync(_conversationId);
    }

    private async Task RetryLastMessageAsync()
    {
        LastError = null;
        await RegenerateLastAssistantAsync();
    }

    private async Task RegenerateLastAssistantAsync()
    {
        if (_chatService is null || _conversationId is null || IsStreaming)
            return;

        var lastUserMessage = Messages.LastOrDefault(message => message.IsUser);
        if (lastUserMessage is null)
            return;

        if (Messages.LastOrDefault() is { IsAssistant: true } assistant)
            Messages.Remove(assistant);

        Messages.Add(new ChatMessageModel
        {
            Role = "assistant",
            Content = string.Empty
        });

        LastError = null;
        IsStreaming = true;
        this.RaisePropertyChanged(nameof(HasMessages));
        UpdateMessageStates();

        await _chatService.SendMessageAsync(_conversationId, lastUserMessage.Content);
    }

    private async Task SendMessageAsync()
    {
        if (_chatService is null || _conversationId is null || string.IsNullOrWhiteSpace(InputText))
            return;

        var userText = InputText.Trim();
        InputText = string.Empty;
        LastError = null;

        Messages.Add(new ChatMessageModel { Role = "user", Content = userText });

        // Add placeholder for streaming assistant response
        Messages.Add(new ChatMessageModel { Role = "assistant", Content = string.Empty });
        IsStreaming = true;
        this.RaisePropertyChanged(nameof(HasMessages));
        UpdateMessageStates();

        await _chatService.SendMessageAsync(_conversationId, userText);
    }

    private void OnTokenReceived(ChatStreamToken token)
    {
        if (token.ConversationId != _conversationId) return;

        Dispatcher.UIThread.Post(() =>
        {
            // Re-check after dispatch: SetConversation may have changed _conversationId
            // between the outer check and this callback executing on the UI thread.
            if (token.ConversationId != _conversationId) return;

            if (token.Error is not null && Messages.Count > 0)
            {
                var last = Messages[^1];
                if (last.IsAssistant)
                {
                    last.Content = token.Error;
                    last.IsStreamingMessage = false;
                    // Trigger UI update by replacing the item
                    Messages[^1] = last;
                }

                LastError = ClassifyError(token.Error);
                IsStreaming = false;
                UpdateMessageStates();
                return;
            }

            if (token.IsComplete)
            {
                IsStreaming = false;

                if (token.WasCancelled)
                {
                    // Remove the empty assistant placeholder if nothing was streamed before stop
                    if (Messages.Count > 0 && Messages[^1].IsAssistant && string.IsNullOrEmpty(Messages[^1].Content))
                        Messages.RemoveAt(Messages.Count - 1);

                    Messages.Add(new ChatMessageModel { Role = "stopped", Content = string.Empty });
                }

                UpdateMessageStates();
                return;
            }

            if (Messages.Count > 0)
            {
                var last = Messages[^1];
                if (last.IsAssistant)
                {
                    last.Content += token.Token;
                    last.IsStreamingMessage = true;
                    // Trigger UI update by replacing the item
                    Messages[^1] = last;
                }
            }

            UpdateMessageStates();
        });
    }

    private void AttachContext()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            InputText = "@attach ";
            return;
        }

        if (!InputText.Contains("@attach", StringComparison.Ordinal))
            InputText = $"{InputText.TrimEnd()}\n@attach ";
    }

    private void UseStarterPrompt(StarterPrompt? prompt)
    {
        if (prompt is null)
            return;

        InputText = prompt.Prompt;
    }

    public void ApplySelectedSlashCommand()
    {
        ApplySlashCommand(SelectedSlashCommandSuggestion);
    }

    private void ApplySlashCommand(SlashCommandSuggestion? suggestion)
    {
        if (suggestion is null)
            return;

        _isApplyingSlashCommand = true;
        try
        {
            InputText = suggestion.InsertText;
            SelectedSlashCommandSuggestion = suggestion;
            VisibleSlashCommandSuggestions.Clear();
        }
        finally
        {
            _isApplyingSlashCommand = false;
            this.RaisePropertyChanged(nameof(IsSlashCommandMenuOpen));
        }
    }

    private void CopyMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        LastCopiedText = content;
    }

    private void UpdateSlashSuggestions()
    {
        VisibleSlashCommandSuggestions.Clear();

        if (!InputText.StartsWith("/", StringComparison.Ordinal) ||
            ContainsSlashTermTerminator(InputText.AsSpan(1)))
        {
            this.RaisePropertyChanged(nameof(IsSlashCommandMenuOpen));
            SelectedSlashCommandSuggestion = null;
            return;
        }

        var query = InputText.Trim().ToLowerInvariant();
        var term = query[1..];
        var matches = SlashCommandSuggestions
            .Where(suggestion =>
                suggestion.Command.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                suggestion.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var suggestion in matches)
            VisibleSlashCommandSuggestions.Add(suggestion);

        SelectedSlashCommandSuggestion = VisibleSlashCommandSuggestions.FirstOrDefault();
        this.RaisePropertyChanged(nameof(IsSlashCommandMenuOpen));
    }

    private void UpdateMessageStates()
    {
        var latestAssistant = Messages.LastOrDefault(message => message.IsAssistant);
        if (!ReferenceEquals(_latestAssistantMessage, latestAssistant))
        {
            if (_latestAssistantMessage is not null)
            {
                _latestAssistantMessage.IsLatestAssistant = false;
                _latestAssistantMessage.IsStreamingMessage = false;
            }

            _latestAssistantMessage = latestAssistant;
            if (_latestAssistantMessage is not null)
                _latestAssistantMessage.IsLatestAssistant = true;
        }

        if (_latestAssistantMessage is not null)
            _latestAssistantMessage.IsStreamingMessage = IsStreaming;

        this.RaisePropertyChanged(nameof(HasMessages));
        this.RaisePropertyChanged(nameof(CanRegenerate));
    }

    private static ChatErrorState ClassifyError(string error)
    {
        var lowered = error.ToLowerInvariant();
        var kind = lowered switch
        {
            var message when message.Contains("api key", StringComparison.Ordinal) ||
                             message.Contains("not configured", StringComparison.Ordinal) =>
                ChatErrorKind.MissingApiKey,
            var message when message.Contains("rate", StringComparison.Ordinal) ||
                             message.Contains("429", StringComparison.Ordinal) =>
                ChatErrorKind.RateLimited,
            var message when message.Contains("network", StringComparison.Ordinal) ||
                             message.Contains("timeout", StringComparison.Ordinal) ||
                             message.Contains("connection", StringComparison.Ordinal) =>
                ChatErrorKind.Network,
            _ => ChatErrorKind.ProviderDown
        };

        return new ChatErrorState
        {
            Kind = kind,
            Message = error
        };
    }

    private static bool ContainsSlashTermTerminator(ReadOnlySpan<char> span) =>
        span.IndexOfAny(stackalloc[] { ' ', '\n', '\r', '\t' }) >= 0;
}
