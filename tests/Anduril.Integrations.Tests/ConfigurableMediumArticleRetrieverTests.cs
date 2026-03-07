using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class ConfigurableMediumArticleRetrieverTests
{
    [Test]
    public async Task FetchAsync_WithHttpOnlyMode_UsesHttpRetriever()
    {
        var httpCalls = 0;
        var browserCalls = 0;
        var retriever = CreateRetriever(
            MediumArticleRetrievalMode.HttpOnly,
            () => httpCalls++,
            () => browserCalls++);

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(httpCalls).IsEqualTo(1);
        await Assert.That(browserCalls).IsEqualTo(0);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Http);
    }

    [Test]
    public async Task FetchAsync_WithBrowserOnlyMode_UsesBrowserRetriever()
    {
        var httpCalls = 0;
        var browserCalls = 0;
        var retriever = CreateRetriever(
            MediumArticleRetrievalMode.BrowserOnly,
            () => httpCalls++,
            () => browserCalls++,
            browserUserDataDirectory: "profiles/medium");

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(httpCalls).IsEqualTo(0);
        await Assert.That(browserCalls).IsEqualTo(1);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Browser);
    }

    [Test]
    public async Task FetchAsync_WithBrowserOnlyMode_AndRemoteDebuggingUrl_UsesBrowserRetriever()
    {
        var httpCalls = 0;
        var browserCalls = 0;
        var retriever = CreateRetriever(
            MediumArticleRetrievalMode.BrowserOnly,
            () => httpCalls++,
            () => browserCalls++,
            browserRemoteDebuggingUrl: "http://127.0.0.1:9222");

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(httpCalls).IsEqualTo(0);
        await Assert.That(browserCalls).IsEqualTo(1);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Browser);
    }

    [Test]
    public async Task FetchAsync_WithAutoMode_WhenHttpSucceeds_DoesNotUseBrowser()
    {
        var httpCalls = 0;
        var browserCalls = 0;
        var retriever = CreateRetriever(
            MediumArticleRetrievalMode.Auto,
            () => httpCalls++,
            () => browserCalls++,
            browserUserDataDirectory: "profiles/medium");

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(httpCalls).IsEqualTo(1);
        await Assert.That(browserCalls).IsEqualTo(0);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Http);
    }

    [Test]
    public async Task FetchAsync_WithAutoMode_WhenHttpHitsCloudflare_UsesBrowserRetriever()
    {
        var browserCalls = 0;
        var retriever = new ConfigurableMediumArticleRetriever(
            Options.Create(new MediumArticleToolOptions
            {
                RetrievalMode = MediumArticleRetrievalMode.Auto,
                BrowserUserDataDirectory = "profiles/medium"
            }),
            new DelegateMediumArticleRetriever((uri, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Http,
                MediumArticleFetchFailureReason.CloudflareChallenge))),
            new DelegateMediumArticleRetriever((uri, _) =>
            {
                browserCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            }));

        var result = await retriever.FetchAsync(new Uri("https://awstip.com/post"), CancellationToken.None);

        await Assert.That(browserCalls).IsEqualTo(1);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Browser);
    }

    [Test]
    public async Task FetchAsync_WithAutoMode_WithoutBrowserConfiguration_ReturnsHttpFailure()
    {
        var browserCalls = 0;
        var retriever = new ConfigurableMediumArticleRetriever(
            Options.Create(new MediumArticleToolOptions
            {
                RetrievalMode = MediumArticleRetrievalMode.Auto,
                BrowserUserDataDirectory = null,
                BrowserRemoteDebuggingUrl = null,
            }),
            new DelegateMediumArticleRetriever((uri, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Http,
                MediumArticleFetchFailureReason.CloudflareChallenge))),
            new DelegateMediumArticleRetriever((uri, _) =>
            {
                browserCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            }));

        var result = await retriever.FetchAsync(new Uri("https://awstip.com/post"), CancellationToken.None);

        await Assert.That(browserCalls).IsEqualTo(0);
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Http);
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.CloudflareChallenge);
    }

    [Test]
    public async Task FetchAsync_WithBrowserOnlyMode_WithoutBrowserProfile_ReturnsBrowserUnavailable()
    {
        var retriever = CreateRetriever(
            MediumArticleRetrievalMode.BrowserOnly,
            static () => { },
            static () => { });

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Browser);
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.BrowserUnavailable);
    }

    private static ConfigurableMediumArticleRetriever CreateRetriever(
        MediumArticleRetrievalMode retrievalMode,
        Action onHttpFetch,
        Action onBrowserFetch,
        string? browserUserDataDirectory = null,
        string? browserRemoteDebuggingUrl = null) =>
        new(
            Options.Create(new MediumArticleToolOptions
            {
                RetrievalMode = retrievalMode,
                BrowserUserDataDirectory = browserUserDataDirectory,
                BrowserRemoteDebuggingUrl = browserRemoteDebuggingUrl,
            }),
            new DelegateMediumArticleRetriever((uri, _) =>
            {
                onHttpFetch();
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Http));
            }),
            new DelegateMediumArticleRetriever((uri, _) =>
            {
                onBrowserFetch();
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            }));

    private sealed class DelegateMediumArticleRetriever(
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchAsync) : IMediumArticleRetriever
    {
        public Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken) =>
            fetchAsync(uri, cancellationToken);
    }
}
