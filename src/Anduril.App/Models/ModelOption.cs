namespace Anduril.App.Models;

/// <summary>
/// Represents a selectable AI provider/model in the model selector dropdown.
/// Uses record for value-based equality so ComboBox selection works after collection replacement.
/// </summary>
public sealed record ModelOption
{
    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }
    public required string ModelName { get; init; }
    public bool IsAvailable { get; init; }
}
