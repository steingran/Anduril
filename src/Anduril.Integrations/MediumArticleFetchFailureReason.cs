namespace Anduril.Integrations;

internal enum MediumArticleFetchFailureReason
{
    None,
    Timeout,
    Forbidden,
    CloudflareChallenge,
    AuthenticationRequired,
    NonMediumPage,
    NetworkError,
    BrowserUnavailable,
    Unknown,
}
