using System.Net;

namespace Anduril.Integrations;

internal sealed record MediumArticleFetchResult
{
    public required bool Success { get; init; }

    public required Uri OriginalUrl { get; init; }

    public required Uri FinalUrl { get; init; }

    public string Html { get; init; } = string.Empty;

    public required MediumArticleRetrievalMethod RetrievalMethod { get; init; }

    public MediumArticleFetchFailureReason FailureReason { get; init; } = MediumArticleFetchFailureReason.None;

    public HttpStatusCode? StatusCode { get; init; }

    public string? DiagnosticMessage { get; init; }

    public static MediumArticleFetchResult Successful(
        Uri originalUrl,
        Uri finalUrl,
        string html,
        MediumArticleRetrievalMethod retrievalMethod,
        HttpStatusCode? statusCode = null) =>
        new()
        {
            Success = true,
            OriginalUrl = originalUrl,
            FinalUrl = finalUrl,
            Html = html,
            RetrievalMethod = retrievalMethod,
            StatusCode = statusCode,
        };

    public static MediumArticleFetchResult Failed(
        Uri originalUrl,
        Uri finalUrl,
        MediumArticleRetrievalMethod retrievalMethod,
        MediumArticleFetchFailureReason failureReason,
        HttpStatusCode? statusCode = null,
        string? diagnosticMessage = null,
        string html = "") =>
        new()
        {
            Success = false,
            OriginalUrl = originalUrl,
            FinalUrl = finalUrl,
            Html = html,
            RetrievalMethod = retrievalMethod,
            FailureReason = failureReason,
            StatusCode = statusCode,
            DiagnosticMessage = diagnosticMessage,
        };
}