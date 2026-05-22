using System.Windows.Input;
using ReactiveUI;

namespace Anduril.App.Models;

/// <summary>
/// Represents a pending file operation proposed by the code agent,
/// waiting for the user to accept or reject it.
/// </summary>
public sealed class StagedActionModel : ReactiveObject
{
    private const int ExpandedDiffThreshold = 18;

    private bool _isExpanded;

    public StagedActionModel()
    {
        ToggleDiffExpansionCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);
    }

    /// <summary>Repo-relative path of the file being created, edited, or deleted.</summary>
    public required string FilePath { get; init; }

    public required StagedActionKind Kind { get; init; }

    /// <summary>Diff lines that describe the change. Empty for pure deletions.</summary>
    public IReadOnlyList<DiffLine> DiffLines { get; init; } = [];

    public bool HasDiffOverflow => DiffLines.Count > ExpandedDiffThreshold;

    public IReadOnlyList<DiffLine> VisibleDiffLines =>
        !HasDiffOverflow || IsExpanded ? DiffLines : DiffLines.Take(ExpandedDiffThreshold).ToArray();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isExpanded, value);
            this.RaisePropertyChanged(nameof(VisibleDiffLines));
            this.RaisePropertyChanged(nameof(ShowDiffExpansionLabel));
        }
    }

    public string ShowDiffExpansionLabel => IsExpanded ? "Show less" : "Show full diff";

    public ICommand ToggleDiffExpansionCommand { get; }
}
