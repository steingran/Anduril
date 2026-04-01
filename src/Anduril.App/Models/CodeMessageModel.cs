using System.Windows.Input;
using ReactiveUI;

namespace Anduril.App.Models;

/// <summary>
/// UI-layer model for a single message in the code-agent conversation.
/// Extends the basic chat message with code-block awareness.
/// </summary>
public sealed class CodeMessageModel : ReactiveObject
{
    private const int CollapseThreshold = 500;

    private bool _isExpanded;

    public CodeMessageModel()
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

    /// <summary>Programming language for syntax context (e.g. "csharp", "python").</summary>
    public string? Language { get; init; }

    /// <summary>True when the content is a code block rather than plain prose.</summary>
    public bool IsCode { get; init; }

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
