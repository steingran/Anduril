using System.Globalization;
using Anduril.Core.MenuPlanning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Anduril.Host;

/// <summary>
/// SQLite-backed store for weekly menu planner subscriptions.
/// </summary>
public sealed class SqliteWeeklyMenuSubscriptionStore : IWeeklyMenuSubscriptionStore
{
    private const int BusyTimeoutMilliseconds = 5000;
    private const int DefaultCommandTimeoutSeconds = 30;
    private readonly string _databasePath;

    public SqliteWeeklyMenuSubscriptionStore(IOptions<WeeklyMenuPlannerOptions> options)
    {
        _databasePath = options.Value.DatabasePath;

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        InitializeDatabase();
    }

    public async Task<WeeklyMenuSubscription?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, RecipientEmail, PreferenceSummary, PeopleCount, MealComplexity,
                   IncludeShoppingList, DeliverySchedule, IsRecurringEnabled,
                   LastDeliveredAtUtc, UpdatedAtUtc
            FROM WeeklyMenuSubscriptions
            WHERE UserId = $userId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$userId", normalizedUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSubscription(reader);
    }

    public async Task UpsertAsync(WeeklyMenuSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var normalizedUserId = NormalizeUserId(subscription.UserId);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WeeklyMenuSubscriptions (
                UserId,
                RecipientEmail,
                PreferenceSummary,
                PeopleCount,
                MealComplexity,
                IncludeShoppingList,
                DeliverySchedule,
                IsRecurringEnabled,
                LastDeliveredAtUtc,
                UpdatedAtUtc)
            VALUES (
                $userId,
                $recipientEmail,
                $preferenceSummary,
                $peopleCount,
                $mealComplexity,
                $includeShoppingList,
                $deliverySchedule,
                $isRecurringEnabled,
                $lastDeliveredAtUtc,
                $updatedAtUtc)
            ON CONFLICT(UserId) DO UPDATE SET
                RecipientEmail = excluded.RecipientEmail,
                PreferenceSummary = excluded.PreferenceSummary,
                PeopleCount = excluded.PeopleCount,
                MealComplexity = excluded.MealComplexity,
                IncludeShoppingList = excluded.IncludeShoppingList,
                DeliverySchedule = excluded.DeliverySchedule,
                IsRecurringEnabled = excluded.IsRecurringEnabled,
                LastDeliveredAtUtc = excluded.LastDeliveredAtUtc,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$userId", normalizedUserId);
        command.Parameters.AddWithValue("$recipientEmail", (object?)subscription.RecipientEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("$preferenceSummary", subscription.PreferenceSummary);
        command.Parameters.AddWithValue("$peopleCount", subscription.PeopleCount);
        command.Parameters.AddWithValue("$mealComplexity", subscription.MealComplexity);
        command.Parameters.AddWithValue("$includeShoppingList", subscription.IncludeShoppingList ? 1 : 0);
        command.Parameters.AddWithValue("$deliverySchedule", subscription.DeliverySchedule);
        command.Parameters.AddWithValue("$isRecurringEnabled", subscription.IsRecurringEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$lastDeliveredAtUtc", subscription.LastDeliveredAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", subscription.UpdatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DisableRecurringAsync(string userId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE WeeklyMenuSubscriptions
            SET IsRecurringEnabled = 0,
                UpdatedAtUtc = $updatedAtUtc
            WHERE UserId = $userId;
            """;
        command.Parameters.AddWithValue("$userId", normalizedUserId);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeeklyMenuSubscription>> ListRecurringAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, RecipientEmail, PreferenceSummary, PeopleCount, MealComplexity,
                   IncludeShoppingList, DeliverySchedule, IsRecurringEnabled,
                   LastDeliveredAtUtc, UpdatedAtUtc
            FROM WeeklyMenuSubscriptions
            WHERE IsRecurringEnabled = 1
            ORDER BY UserId;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var subscriptions = new List<WeeklyMenuSubscription>();

        while (await reader.ReadAsync(cancellationToken))
            subscriptions.Add(MapSubscription(reader));

        return subscriptions;
    }

    public async Task MarkDeliveredAsync(string userId, DateTime deliveredAtUtc, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE WeeklyMenuSubscriptions
            SET LastDeliveredAtUtc = $lastDeliveredAtUtc,
                UpdatedAtUtc = $updatedAtUtc
            WHERE UserId = $userId;
            """;
        command.Parameters.AddWithValue("$userId", normalizedUserId);
        command.Parameters.AddWithValue("$lastDeliveredAtUtc", deliveredAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();

        using var journalModeCommand = connection.CreateCommand();
        journalModeCommand.CommandText = "PRAGMA journal_mode = WAL;";
        _ = journalModeCommand.ExecuteScalar();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS WeeklyMenuSubscriptions (
                UserId TEXT NOT NULL PRIMARY KEY,
                RecipientEmail TEXT NULL,
                PreferenceSummary TEXT NOT NULL,
                PeopleCount INTEGER NOT NULL,
                MealComplexity TEXT NOT NULL,
                IncludeShoppingList INTEGER NOT NULL,
                DeliverySchedule TEXT NOT NULL,
                IsRecurringEnabled INTEGER NOT NULL,
                LastDeliveredAtUtc TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        return connection;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = CreateConnection();
        connection.Open();
        ConfigureConnection(connection);
        return connection;
    }

    private SqliteConnection CreateConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
            DefaultTimeout = DefaultCommandTimeoutSeconds
        }.ToString();

        return new SqliteConnection(connectionString);
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ConfigureConnection(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
        command.ExecuteNonQuery();
    }

    private static string NormalizeUserId(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return userId.Trim();
    }

    private static WeeklyMenuSubscription MapSubscription(SqliteDataReader reader)
    {
        DateTime? lastDeliveredAtUtc = reader.IsDBNull(reader.GetOrdinal("LastDeliveredAtUtc"))
            ? null
            : DateTime.Parse(
                reader.GetString(reader.GetOrdinal("LastDeliveredAtUtc")),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        string? recipientEmail = reader.IsDBNull(reader.GetOrdinal("RecipientEmail"))
            ? null
            : reader.GetString(reader.GetOrdinal("RecipientEmail"));

        return new WeeklyMenuSubscription
        {
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            RecipientEmail = recipientEmail,
            PreferenceSummary = reader.GetString(reader.GetOrdinal("PreferenceSummary")),
            PeopleCount = reader.GetInt32(reader.GetOrdinal("PeopleCount")),
            MealComplexity = reader.GetString(reader.GetOrdinal("MealComplexity")),
            IncludeShoppingList = reader.GetInt32(reader.GetOrdinal("IncludeShoppingList")) == 1,
            DeliverySchedule = reader.GetString(reader.GetOrdinal("DeliverySchedule")),
            IsRecurringEnabled = reader.GetInt32(reader.GetOrdinal("IsRecurringEnabled")) == 1,
            LastDeliveredAtUtc = lastDeliveredAtUtc,
            UpdatedAtUtc = DateTime.Parse(
                reader.GetString(reader.GetOrdinal("UpdatedAtUtc")),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
        };
    }
}
