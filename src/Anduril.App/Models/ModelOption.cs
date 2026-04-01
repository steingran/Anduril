namespace Anduril.App.Models;

/// <summary>
/// Represents a selectable AI provider/model in the model selector dropdown.
/// </summary>
public sealed class ModelOption
{
    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }
    public required string ModelName { get; init; }
    public bool IsAvailable { get; init; }
}
