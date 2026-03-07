namespace Anduril.Integrations;

internal interface IMediumArticleRetriever
{
    Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken);
}
