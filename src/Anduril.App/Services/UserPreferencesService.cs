using System.Text.Json;
using Anduril.App.Models;

namespace Anduril.App.Services;

/// <summary>
/// Loads and saves user preferences to a JSON file in the OS application-data directory.
/// All operations are best-effort: failures are silently swallowed so they never
/// disrupt the application startup or model-selection flow.
/// </summary>
public sealed class UserPreferencesService : IUserPreferencesService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Anduril",
        "preferences.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Synchronously loads preferences from disk.
    /// Returns a default instance when the file is absent or unreadable.
    /// </summary>
    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UserPreferences();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    /// <summary>
    /// Asynchronously persists preferences to disk.
    /// Creates the directory if it does not yet exist.
    /// </summary>
    public async Task SaveAsync(UserPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(preferences, JsonOptions);
            await File.WriteAllTextAsync(FilePath, json);
        }
        catch
        {
            // Best-effort — ignore errors
        }
    }
}

