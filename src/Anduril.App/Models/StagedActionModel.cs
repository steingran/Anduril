namespace Anduril.App.Models;

/// <summary>
/// Represents a pending file operation proposed by the code agent,
/// waiting for the user to accept or reject it.
/// </summary>
public sealed class StagedActionModel
{
    /// <summary>Repo-relative path of the file being created, edited, or deleted.</summary>
    public required string FilePath { get; init; }

    public required StagedActionKind Kind { get; init; }

    /// <summary>Diff lines that describe the change. Empty for pure deletions.</summary>
    public IReadOnlyList<DiffLine> DiffLines { get; init; } = [];
}

