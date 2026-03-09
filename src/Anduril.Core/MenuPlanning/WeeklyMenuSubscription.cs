namespace Anduril.Core.MenuPlanning;

/// <summary>
/// Represents a user's saved menu-planning preferences and optional recurring email settings.
/// </summary>
public sealed class WeeklyMenuSubscription
{
    public required string UserId { get; init; }

    public string? RecipientEmail { get; init; }

    public required string PreferenceSummary { get; init; }

    public int PeopleCount { get; init; } = 2;

    public string MealComplexity { get; init; } = "balanced";

    public bool IncludeShoppingList { get; init; } = true;

    public string DeliverySchedule { get; init; } = "0 18 * * 0";

    public bool IsRecurringEnabled { get; init; }

    public DateTime? LastDeliveredAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}