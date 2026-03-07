using Microsoft.Extensions.Options;

namespace Anduril.Integrations;

internal sealed class ConfigurableMediumArticleRetriever(
    IOptions<MediumArticleToolOptions> options,
    IMediumArticleRetriever httpRetriever,
    IMediumArticleRetriever browserRetriever) : IMediumArticleRetriever
{
    private readonly MediumArticleToolOptions _options = options.Value;

    public async Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        return _options.RetrievalMode switch
        {
            MediumArticleRetrievalMode.BrowserOnly => await FetchWithBrowserAsync(uri, cancellationToken),
            MediumArticleRetrievalMode.Auto => await FetchAutomaticallyAsync(uri, cancellationToken),
            _ => await httpRetriever.FetchAsync(uri, cancellationToken)
        };
    }

    private async Task<MediumArticleFetchResult> FetchAutomaticallyAsync(Uri uri, CancellationToken cancellationToken)
    {
        var httpResult = await httpRetriever.FetchAsync(uri, cancellationToken);

        if (httpResult.Success || !ShouldFallbackToBrowser(httpResult) || !MediumArticleBrowserConfiguration.IsConfigured(_options))
            return httpResult;

        return await browserRetriever.FetchAsync(uri, cancellationToken);
    }

    private async Task<MediumArticleFetchResult> FetchWithBrowserAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!MediumArticleBrowserConfiguration.IsConfigured(_options))
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable,
                diagnosticMessage: MediumArticleBrowserConfiguration.MissingConfigurationMessage);
        }

        return await browserRetriever.FetchAsync(uri, cancellationToken);
    }

    private static bool ShouldFallbackToBrowser(MediumArticleFetchResult result) =>
        result.FailureReason is MediumArticleFetchFailureReason.CloudflareChallenge or
            MediumArticleFetchFailureReason.Forbidden or
            MediumArticleFetchFailureReason.AuthenticationRequired;
}
