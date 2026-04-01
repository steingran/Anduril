using System.Windows.Input;
using ReactiveUI;

namespace Anduril.App.Models;

/// <summary>
/// UI-layer model representing a single chat message displayed in the conversation view.
/// </summary>
public sealed class ChatMessageModel : ReactiveObject
{
    private const int CollapseThreshold = 500;

    private bool _isExpanded;

    public ChatMessageModel()
    {
        ToggleExpandCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);
    }

    private string _content = string.Empty;

    public required string Role { get; init; }

    public required string Content
    {
        get => _content;
        set
        {
            this.RaiseAndSetIfChanged(ref _content, value);
            this.RaisePropertyChanged(nameof(IsLoading));
            this.RaisePropertyChanged(nameof(IsCollapsible));
            this.RaisePropertyChanged(nameof(ContentMaxHeight));
        }
    }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsStopped => Role == "stopped";
    public bool IsLoading => IsAssistant && string.IsNullOrEmpty(Content);

    public bool IsCollapsible => Content.Length > CollapseThreshold;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(ContentMaxHeight));
            this.RaisePropertyChanged(nameof(ShowMoreLabel));
        }
    }

    public double ContentMaxHeight => IsCollapsible && !IsExpanded ? 200.0 : double.PositiveInfinity;
    public string ShowMoreLabel => IsExpanded ? "Show less" : "Show more";

    public ICommand ToggleExpandCommand { get; }
}
