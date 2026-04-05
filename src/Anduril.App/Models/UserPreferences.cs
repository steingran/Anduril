namespace Anduril.App.Models;

/// <summary>
/// User-level preferences persisted across application restarts.
/// Stored as JSON in the OS application-data directory.
/// </summary>
public sealed class UserPreferences
{
    /// <summary>
    /// Gets or sets the provider ID last selected in the model selector
    /// (e.g., "anthropic::claude-3-haiku-20240307" or "copilot::gpt-4o").
    /// Null when no preference has been saved yet.
    /// </summary>
    public string? SelectedProviderId { get; set; }
}

