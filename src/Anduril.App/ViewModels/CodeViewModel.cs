using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using Anduril.App.Models;
using Anduril.App.Services;
using Anduril.Core.Communication;
using Avalonia.Threading;
using ReactiveUI;

namespace Anduril.App.ViewModels;

/// <summary>
/// ViewModel for the Code Agent view.
/// Sends messages through the shared <see cref="SignalRChatService"/> on its own
/// conversation, and manages staged file-action proposals from the agent.
/// </summary>
public sealed class CodeViewModel : ViewModelBase
{
    private IChatService? _chatService;
    private string? _conversationId;
    private string _inputText = string.Empty;
    private bool _isStreaming;
    private bool _hasStagedActions;
    private string? _selectedRepoPath;
    private string? _selectedBranch;

    public CodeViewModel()
    {
        SendCommand = ReactiveCommand.CreateFromTask(
            SendMessageAsync,
            this.WhenAnyValue(x => x.InputText, x => x.IsStreaming,
                (text, streaming) => !string.IsNullOrWhiteSpace(text) && !streaming));

        StopCommand = ReactiveCommand.CreateFromTask(
            StopStreamingAsync,
            this.WhenAnyValue(x => x.IsStreaming));

        var hasStagedActions = this.WhenAnyValue(x => x.HasStagedActions);
        AcceptAllCommand = ReactiveCommand.Create(AcceptAll, hasStagedActions);
        RejectAllCommand = ReactiveCommand.Create(RejectAll, hasStagedActions);
    }

    public ObservableCollection<CodeMessageModel> Messages { get; } = [];
    public ObservableCollection<StagedActionModel> StagedActions { get; } = [];
    public ObservableCollection<string> AvailableBranches { get; } = [];

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

    /// <summary>True when the agent has proposed at least one staged file action.</summary>
    public bool HasStagedActions
    {
        get => _hasStagedActions;
        set => this.RaiseAndSetIfChanged(ref _hasStagedActions, value);
    }

    /// <summary>The local path of the repository selected by the user. Null until chosen.</summary>
    public string? SelectedRepoPath
    {
        get => _selectedRepoPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRepoPath, value);
            this.RaisePropertyChanged(nameof(HasSelectedRepo));
            this.RaisePropertyChanged(nameof(RepoDisplayName));

            if (value is not null)
                _ = LoadBranchesAsync(value);
            else
            {
                AvailableBranches.Clear();
                SelectedBranch = null;
            }
        }
    }

    /// <summary>The branch currently selected in the branch picker. Null until a repo is chosen.</summary>
    public string? SelectedBranch
    {
        get => _selectedBranch;
        set => this.RaiseAndSetIfChanged(ref _selectedBranch, value);
    }

    /// <summary>True once a repo folder has been selected.</summary>
    public bool HasSelectedRepo => _selectedRepoPath is not null;

    /// <summary>The folder name shown in the repo bar (last path segment).</summary>
    public string RepoDisplayName => _selectedRepoPath is not null
        ? Path.GetFileName(_selectedRepoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        : string.Empty;

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> AcceptAllCommand { get; }
    public ReactiveCommand<Unit, Unit> RejectAllCommand { get; }

    /// <summary>Wires the view-model to a conversation on the shared chat service.</summary>
    public void SetConversation(string conversationId, IChatService chatService)
    {
        if (_chatService is not null)
            _chatService.TokenReceived -= OnTokenReceived;

        _conversationId = conversationId;
        _chatService = chatService;
        _chatService.TokenReceived += OnTokenReceived;

        Messages.Clear();
        StagedActions.Clear();
        HasStagedActions = false;
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

        Messages.Add(new CodeMessageModel { Role = "user", Content = userText });
        Messages.Add(new CodeMessageModel { Role = "assistant", Content = string.Empty });
        IsStreaming = true;

        await _chatService.SendMessageAsync(_conversationId, userText, repoPath: _selectedRepoPath, branchName: _selectedBranch);
    }

    /// <summary>
    /// Runs <c>git branch</c> in the selected repository to populate <see cref="AvailableBranches"/>
    /// and pre-select the current branch. Silently no-ops if git is unavailable or the path is
    /// not a git repository.
    /// </summary>
    private async Task LoadBranchesAsync(string repoPath)
    {
        try
        {
            // Use ArgumentList (not Arguments string) so paths with trailing backslashes
            // or spaces are always quoted correctly by the runtime — no manual escaping needed.

            // List all local branches
            var listPsi = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            listPsi.ArgumentList.Add("-C");
            listPsi.ArgumentList.Add(repoPath);
            listPsi.ArgumentList.Add("branch");
            listPsi.ArgumentList.Add("--format");
            listPsi.ArgumentList.Add("%(refname:short)");

            using var listProcess = Process.Start(listPsi);
            if (listProcess is null) return;

            var listOutput = await listProcess.StandardOutput.ReadToEndAsync();
            await listProcess.WaitForExitAsync();

            // Detect the currently checked-out branch
            var currentPsi = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            currentPsi.ArgumentList.Add("-C");
            currentPsi.ArgumentList.Add(repoPath);
            currentPsi.ArgumentList.Add("branch");
            currentPsi.ArgumentList.Add("--show-current");

            using var currentProcess = Process.Start(currentPsi);
            var currentBranch = string.Empty;
            if (currentProcess is not null)
            {
                currentBranch = (await currentProcess.StandardOutput.ReadToEndAsync()).Trim();
                await currentProcess.WaitForExitAsync();
            }

            var branches = listOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                AvailableBranches.Clear();
                foreach (var branch in branches)
                    AvailableBranches.Add(branch);

                SelectedBranch = AvailableBranches.FirstOrDefault(b => b == currentBranch)
                    ?? AvailableBranches.FirstOrDefault();
            });
        }
        catch
        {
            // git not available or path is not a repository — branch selector stays empty
        }
    }

    private void AcceptAll()
    {
        StagedActions.Clear();
        HasStagedActions = false;
    }

    private void RejectAll()
    {
        StagedActions.Clear();
        HasStagedActions = false;
    }

    private void OnTokenReceived(ChatStreamToken token)
    {
        if (token.ConversationId != _conversationId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (token.Error is not null && Messages.Count > 0)
            {
                var last = Messages[^1];
                if (last.IsAssistant)
                {
                    last.Content = token.Error;
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

                    Messages.Add(new CodeMessageModel { Role = "stopped", Content = string.Empty });
                }

                return;
            }

            if (Messages.Count > 0)
            {
                var last = Messages[^1];
                if (last.IsAssistant)
                {
                    last.Content += token.Token;
                    Messages[^1] = last;
                }
            }
        });
    }

    private void OnStagedActionReceived(StagedActionModel action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StagedActions.Add(action);
            HasStagedActions = true;
        });
    }
}
