namespace Anduril.App.Models;

/// <summary>A single line in a unified diff with its classification.</summary>
public sealed record DiffLine(DiffLineKind Kind, string Content);

