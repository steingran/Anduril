using Anduril.App.Views.Controls;

namespace Anduril.App.Models;

public enum ChatErrorKind
{
    ProviderDown,
    RateLimited,
    MissingApiKey,
    Network
}

public sealed record ChatErrorState
{
    public required ChatErrorKind Kind { get; init; }

    public required string Message { get; init; }

    public DateTimeOffset? RetryAt { get; init; }

    public string? ProviderId { get; init; }

    public string Title => Kind switch
    {
        ChatErrorKind.MissingApiKey => "Provider setup required",
        ChatErrorKind.RateLimited => "Rate limit reached",
        ChatErrorKind.Network => "Connection problem",
        _ => "Provider unavailable"
    };

    public AndurilAlertVariant Variant => Kind switch
    {
        ChatErrorKind.Network => AndurilAlertVariant.Warning,
        ChatErrorKind.RateLimited => AndurilAlertVariant.Warning,
        _ => AndurilAlertVariant.Danger
    };

    public string? RetryLabel => Kind switch
    {
        ChatErrorKind.ProviderDown => "Retry",
        ChatErrorKind.RateLimited => "Retry later",
        ChatErrorKind.Network => "Retry",
        _ => null
    };

    public string? ConfigureLabel => Kind == ChatErrorKind.MissingApiKey ? "Configure provider" : null;
    public bool HasRetryAction => RetryLabel is not null;
    public bool HasConfigureAction => ConfigureLabel is not null;

    public string DetailText =>
        RetryAt is { } retryAt
            ? $"{Message} Retry after {retryAt.LocalDateTime:t}."
            : Message;
}
