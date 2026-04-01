namespace Anduril.App.Models;

public sealed class ToolInspectorItem
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsNotAvailable => !IsAvailable;
    public List<string> Functions { get; init; } = [];
    public bool HasFunctions => Functions.Count > 0;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool SupportsChatCompletion { get; init; }
}
