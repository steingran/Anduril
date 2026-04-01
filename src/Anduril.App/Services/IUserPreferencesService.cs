using Anduril.App.Models;

namespace Anduril.App.Services;

/// <summary>
/// Abstracts loading and saving user preferences,
/// allowing fake implementations in tests.
/// </summary>
public interface IUserPreferencesService
{
    UserPreferences Load();
    Task SaveAsync(UserPreferences preferences);
}
