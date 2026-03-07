using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

internal sealed class HttpMediumArticleRetriever : IMediumArticleRetriever
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MediumArticleToolOptions _options;

    public HttpMediumArticleRetriever(IOptions<MediumArticleToolOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = CreateRequestMessage(uri);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            using var response = await _httpClientFactory.CreateClient(nameof(MediumArticleTool)).SendAsync(request, cts.Token);
            var finalUrl = response.RequestMessage?.RequestUri ?? uri;
            var html = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cts.Token);

            if (MediumArticleFetchClassifier.IsCloudflareChallenge(html))
            {
                return MediumArticleFetchResult.Failed(
                    uri,
                    finalUrl,
                    MediumArticleRetrievalMethod.Http,
                    MediumArticleFetchFailureReason.CloudflareChallenge,
                    response.StatusCode,
                    html: html);
            }

            if (!response.IsSuccessStatusCode)
            {
                return MediumArticleFetchResult.Failed(
                    uri,
                    finalUrl,
                    MediumArticleRetrievalMethod.Http,
                    MediumArticleFetchClassifier.ClassifyFailure(response.StatusCode, html),
                    response.StatusCode,
                    html: html);
            }

            return MediumArticleFetchResult.Successful(
                uri,
                finalUrl,
                html,
                MediumArticleRetrievalMethod.Http,
                response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Http,
                MediumArticleFetchFailureReason.Timeout);
        }
        catch (HttpRequestException ex)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Http,
                MediumArticleFetchFailureReason.NetworkError,
                diagnosticMessage: ex.Message);
        }
    }

    internal HttpRequestMessage CreateRequestMessage(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent.Trim());

        return request;
    }
}
