using System.Collections.ObjectModel;
using System.Reactive;
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

    public ChatViewModel()
    {
        SendCommand = ReactiveCommand.CreateFromTask(
            SendMessageAsync,
            this.WhenAnyValue(x => x.InputText, x => x.IsStreaming,
                (text, streaming) => !string.IsNullOrWhiteSpace(text) && !streaming));

        StopCommand = ReactiveCommand.CreateFromTask(
            StopStreamingAsync,
            this.WhenAnyValue(x => x.IsStreaming));
    }

    public ObservableCollection<ChatMessageModel> Messages { get; } = [];

    public string InputText
    {
        get => _inputText;
        set => this.RaiseAndSetIfChanged(ref _inputText, value);
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set => this.RaiseAndSetIfChanged(ref _isStreaming, value);
    }

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    public void SetConversation(string conversationId, IChatService chatService)
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
    }

    private async Task StopStreamingAsync()
    {
        if (_chatService is null || _conversationId is null) return;
        await _chatService.CancelMessageAsync(_conversationId);
    }

    private async Task SendMessageAsync()
    {
        if (_chatService is null || _conversationId is null || string.IsNullOrWhiteSpace(InputText))
            return;

        var userText = InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessageModel { Role = "user", Content = userText });

        // Add placeholder for streaming assistant response
        Messages.Add(new ChatMessageModel { Role = "assistant", Content = string.Empty });
        IsStreaming = true;

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
                    // Trigger UI update by replacing the item
                    Messages[^1] = last;
                }
                IsStreaming = false;
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

                return;
            }

            if (Messages.Count > 0)
            {
                var last = Messages[^1];
                if (last.IsAssistant)
                {
                    last.Content += token.Token;
                    // Trigger UI update by replacing the item
                    Messages[^1] = last;
                }
            }
        });
    }
}
