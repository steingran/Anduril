using Anduril.App.Models;
using Anduril.App.Services;

namespace Anduril.App.Tests;

/// <summary>
/// In-memory implementation of <see cref="IUserPreferencesService"/> for use in unit tests.
/// Stores preferences in memory rather than on disk.
/// </summary>
public sealed class FakeUserPreferencesService : IUserPreferencesService
{
    private UserPreferences _preferences = new();

    public UserPreferences Load() => _preferences;

    public Task SaveAsync(UserPreferences preferences)
    {
        _preferences = preferences;
        return Task.CompletedTask;
    }
}
