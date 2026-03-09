using Anduril.Core.MenuPlanning;
using Microsoft.Extensions.Options;

namespace Anduril.Host.Tests;

public class SqliteWeeklyMenuSubscriptionStoreTests
{
    [Test]
    public async Task UpsertAndGetAsync_RoundTripsSubscription()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"weekly-menu-{Guid.NewGuid():N}.db");

        try
        {
            var store = CreateStore(databasePath);
            var subscription = new WeeklyMenuSubscription
            {
                UserId = "user-1",
                RecipientEmail = "chef@example.com",
                PreferenceSummary = "Vegetarian dinners, quick lunches, no peanuts.",
                PeopleCount = 3,
                MealComplexity = "quick weeknight meals",
                IncludeShoppingList = true,
                DeliverySchedule = "0 18 * * 0",
                IsRecurringEnabled = true,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await store.UpsertAsync(subscription);
            var saved = await store.GetAsync("user-1");

            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.RecipientEmail).IsEqualTo(subscription.RecipientEmail);
            await Assert.That(saved.PreferenceSummary).IsEqualTo(subscription.PreferenceSummary);
            await Assert.That(saved.PeopleCount).IsEqualTo(3);
            await Assert.That(saved.IsRecurringEnabled).IsTrue();
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Test]
    public async Task DisableRecurringAsync_RemovesSubscriptionFromRecurringList()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"weekly-menu-{Guid.NewGuid():N}.db");

        try
        {
            var store = CreateStore(databasePath);

            await store.UpsertAsync(new WeeklyMenuSubscription
            {
                UserId = "user-2",
                PreferenceSummary = "Vegan and gluten-free.",
                IsRecurringEnabled = true,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await store.DisableRecurringAsync("user-2");
            var recurring = await store.ListRecurringAsync();

            await Assert.That(recurring.Count).IsEqualTo(0);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Test]
    public async Task MarkDeliveredAsync_UpdatesLastDeliveredTimestamp()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"weekly-menu-{Guid.NewGuid():N}.db");
        var deliveredAtUtc = new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc);

        try
        {
            var store = CreateStore(databasePath);

            await store.UpsertAsync(new WeeklyMenuSubscription
            {
                UserId = "user-3",
                PreferenceSummary = "Mediterranean meals.",
                IsRecurringEnabled = true,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await store.MarkDeliveredAsync("user-3", deliveredAtUtc);
            var saved = await store.GetAsync("user-3");

            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.LastDeliveredAtUtc).IsEqualTo(deliveredAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Test]
    public async Task StoreOperations_NormalizeTrimmedUserId()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"weekly-menu-{Guid.NewGuid():N}.db");

        try
        {
            var store = CreateStore(databasePath);
            await store.UpsertAsync(new WeeklyMenuSubscription
            {
                UserId = "  user-4  ",
                PreferenceSummary = "Simple dinners.",
                IsRecurringEnabled = true,
                UpdatedAtUtc = DateTime.UtcNow
            });

            var saved = await store.GetAsync("user-4");
            await store.DisableRecurringAsync("  user-4");
            await store.MarkDeliveredAsync(" user-4 ", new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc));
            var updated = await store.GetAsync("user-4");
            var recurring = await store.ListRecurringAsync();

            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.UserId).IsEqualTo("user-4");
            await Assert.That(updated).IsNotNull();
            await Assert.That(updated!.LastDeliveredAtUtc).IsEqualTo(new DateTime(2026, 03, 08, 18, 05, 00, DateTimeKind.Utc));
            await Assert.That(recurring.Count).IsEqualTo(0);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static SqliteWeeklyMenuSubscriptionStore CreateStore(string databasePath) =>
        new(Options.Create(new WeeklyMenuPlannerOptions { DatabasePath = databasePath }));

    private static void DeleteDatabaseFiles(string databasePath)
    {
        DeleteFileIfExists(databasePath);
        DeleteFileIfExists($"{databasePath}-wal");
        DeleteFileIfExists($"{databasePath}-shm");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
