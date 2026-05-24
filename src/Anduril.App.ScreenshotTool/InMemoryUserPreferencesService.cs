using Anduril.App.Models;
using Anduril.App.Services;

namespace Anduril.App.ScreenshotTool;

internal sealed class InMemoryUserPreferencesService : IUserPreferencesService
{
    private readonly UserPreferences _preferences = new();

    public UserPreferences Load() => _preferences;

    public Task SaveAsync(UserPreferences preferences)
    {
        _preferences.SelectedProviderId = preferences.SelectedProviderId;
        return Task.CompletedTask;
    }
}
