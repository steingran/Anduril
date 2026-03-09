namespace Anduril.Core.MenuPlanning;

/// <summary>
/// Persists saved weekly menu preferences and recurring email delivery settings.
/// </summary>
public interface IWeeklyMenuSubscriptionStore
{
    Task<WeeklyMenuSubscription?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(WeeklyMenuSubscription subscription, CancellationToken cancellationToken = default);

    Task DisableRecurringAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyMenuSubscription>> ListRecurringAsync(CancellationToken cancellationToken = default);

    Task MarkDeliveredAsync(string userId, DateTime deliveredAtUtc, CancellationToken cancellationToken = default);
}