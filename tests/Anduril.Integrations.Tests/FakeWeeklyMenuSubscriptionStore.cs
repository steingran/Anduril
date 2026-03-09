using Anduril.Core.MenuPlanning;

namespace Anduril.Integrations.Tests;

internal sealed class FakeWeeklyMenuSubscriptionStore : IWeeklyMenuSubscriptionStore
{
    private readonly Dictionary<string, WeeklyMenuSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public Task<WeeklyMenuSubscription?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        _subscriptions.TryGetValue(userId, out var subscription);
        return Task.FromResult(subscription);
    }

    public Task UpsertAsync(WeeklyMenuSubscription subscription, CancellationToken cancellationToken = default)
    {
        _subscriptions[subscription.UserId] = subscription;
        return Task.CompletedTask;
    }

    public Task DisableRecurringAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(userId, out var subscription))
            _subscriptions[userId] = new WeeklyMenuSubscription
            {
                UserId = subscription.UserId,
                RecipientEmail = subscription.RecipientEmail,
                PreferenceSummary = subscription.PreferenceSummary,
                PeopleCount = subscription.PeopleCount,
                MealComplexity = subscription.MealComplexity,
                IncludeShoppingList = subscription.IncludeShoppingList,
                DeliverySchedule = subscription.DeliverySchedule,
                IsRecurringEnabled = false,
                LastDeliveredAtUtc = subscription.LastDeliveredAtUtc,
                UpdatedAtUtc = subscription.UpdatedAtUtc
            };

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WeeklyMenuSubscription>> ListRecurringAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WeeklyMenuSubscription>>([.. _subscriptions.Values.Where(subscription => subscription.IsRecurringEnabled)]);

    public Task MarkDeliveredAsync(string userId, DateTime deliveredAtUtc, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(userId, out var subscription))
            _subscriptions[userId] = new WeeklyMenuSubscription
            {
                UserId = subscription.UserId,
                RecipientEmail = subscription.RecipientEmail,
                PreferenceSummary = subscription.PreferenceSummary,
                PeopleCount = subscription.PeopleCount,
                MealComplexity = subscription.MealComplexity,
                IncludeShoppingList = subscription.IncludeShoppingList,
                DeliverySchedule = subscription.DeliverySchedule,
                IsRecurringEnabled = subscription.IsRecurringEnabled,
                LastDeliveredAtUtc = deliveredAtUtc,
                UpdatedAtUtc = subscription.UpdatedAtUtc
            };

        return Task.CompletedTask;
    }
}