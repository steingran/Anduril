namespace Anduril.App.Models;

/// <summary>
/// Lightweight summary of an active tool call for UI chips.
/// </summary>
public sealed record ToolCallSummary
{
    public required string ToolName { get; init; }

    public required string ToolId { get; init; }

    public string? ToolIcon { get; init; }

    public string? Detail { get; init; }
}
