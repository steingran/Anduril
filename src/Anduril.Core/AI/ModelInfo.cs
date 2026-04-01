namespace Anduril.Core.AI;

/// <summary>
/// Describes an AI model available through a provider, with both its technical ID
/// and a human-readable display name.
/// </summary>
public sealed record ModelInfo
{
    /// <summary>
    /// Gets the technical model identifier used in API calls (e.g., "claude-3-haiku-20240307").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the human-readable display name (e.g., "Claude 3 Haiku").
    /// Falls back to <see cref="Id"/> when the provider does not supply a friendly name.
    /// </summary>
    public required string DisplayName { get; init; }
}

