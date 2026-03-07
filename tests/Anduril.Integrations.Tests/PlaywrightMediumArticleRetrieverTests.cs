using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Anduril.Integrations.Tests;

public class PlaywrightMediumArticleRetrieverTests
{
    [Test]
    public async Task FetchAsync_WhenAttachIsUnavailableAndLaunchProfileExists_FallsBackToLaunch()
    {
        var attachCalls = 0;
        var launchCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserRemoteDebuggingUrl = "http://127.0.0.1:9222",
                BrowserUserDataDirectory = "./sessions/medium-browser",
                BrowserManualInterventionWaitSeconds = 1,
                BrowserManualInterventionRetryCount = 1
            },
            (uri, _) =>
            {
                attachCalls++;
                return Task.FromResult(MediumArticleFetchResult.Failed(
                    uri,
                    uri,
                    MediumArticleRetrievalMethod.Browser,
                    MediumArticleFetchFailureReason.BrowserUnavailable,
                    diagnosticMessage: "connect ECONNREFUSED 127.0.0.1:9222"));
            },
            (uri, _) =>
            {
                launchCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            });

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(attachCalls).IsEqualTo(1);
        await Assert.That(launchCalls).IsEqualTo(1);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task FetchAsync_WhenAttachSucceeds_DoesNotLaunch()
    {
        var attachCalls = 0;
        var launchCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserRemoteDebuggingUrl = "http://127.0.0.1:9222",
                BrowserUserDataDirectory = "./sessions/medium-browser",
                BrowserManualInterventionWaitSeconds = 1,
                BrowserManualInterventionRetryCount = 1
            },
            (uri, _) =>
            {
                attachCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            },
            (uri, _) =>
            {
                launchCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            });

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(attachCalls).IsEqualTo(1);
        await Assert.That(launchCalls).IsEqualTo(0);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task FetchAsync_WhenAttachHitsCloudflare_DoesNotLaunch()
    {
        var attachCalls = 0;
        var launchCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserRemoteDebuggingUrl = "http://127.0.0.1:9222",
                BrowserUserDataDirectory = "./sessions/medium-browser",
                BrowserManualInterventionWaitSeconds = 1,
                BrowserManualInterventionRetryCount = 1
            },
            (uri, _) =>
            {
                attachCalls++;
                return Task.FromResult(MediumArticleFetchResult.Failed(
                    uri,
                    uri,
                    MediumArticleRetrievalMethod.Browser,
                    MediumArticleFetchFailureReason.CloudflareChallenge));
            },
            (uri, _) =>
            {
                launchCalls++;
                return Task.FromResult(MediumArticleFetchResult.Successful(uri, uri, "<html />", MediumArticleRetrievalMethod.Browser));
            });

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(attachCalls).IsEqualTo(1);
        await Assert.That(launchCalls).IsEqualTo(0);
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.CloudflareChallenge);
    }

    [Test]
    public async Task ExecuteManualInterventionLoopAsync_WhenChallengeClearsAfterWait_ReturnsSuccessfulRetry()
    {
        var delayCalls = 0;
        var prepareCalls = 0;
        var fetchCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserManualInterventionWaitSeconds = 5,
                BrowserManualInterventionRetryCount = 1
            },
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            });

        var result = await retriever.ExecuteManualInterventionLoopAsync(
            new Uri("https://medium.com/@author/post"),
            browserIsVisible: true,
            (_, _) =>
            {
                fetchCalls++;
                return Task.FromResult(fetchCalls == 1
                    ? MediumArticleFetchResult.Failed(
                        new Uri("https://medium.com/@author/post"),
                        new Uri("https://medium.com/@author/post"),
                        MediumArticleRetrievalMethod.Browser,
                        MediumArticleFetchFailureReason.CloudflareChallenge)
                    : MediumArticleFetchResult.Successful(
                        new Uri("https://medium.com/@author/post"),
                        new Uri("https://medium.com/@author/post"),
                        "<html />",
                        MediumArticleRetrievalMethod.Browser));
            },
            CancellationToken.None,
            _ =>
            {
                prepareCalls++;
                return Task.CompletedTask;
            });

        await Assert.That(fetchCalls).IsEqualTo(2);
        await Assert.That(delayCalls).IsEqualTo(1);
        await Assert.That(prepareCalls).IsEqualTo(1);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ExecuteManualInterventionLoopAsync_WhenBrowserIsNotVisible_DoesNotRetry()
    {
        var delayCalls = 0;
        var fetchCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserManualInterventionWaitSeconds = 5,
                BrowserManualInterventionRetryCount = 1
            },
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            });

        var result = await retriever.ExecuteManualInterventionLoopAsync(
            new Uri("https://medium.com/@author/post"),
            browserIsVisible: false,
            (_, _) =>
            {
                fetchCalls++;
                return Task.FromResult(MediumArticleFetchResult.Failed(
                    new Uri("https://medium.com/@author/post"),
                    new Uri("https://medium.com/@author/post"),
                    MediumArticleRetrievalMethod.Browser,
                    MediumArticleFetchFailureReason.AuthenticationRequired));
            },
            CancellationToken.None);

        await Assert.That(fetchCalls).IsEqualTo(1);
        await Assert.That(delayCalls).IsEqualTo(0);
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.AuthenticationRequired);
    }

    [Test]
    public async Task ExecuteAttachedNavigationAttemptAsync_WhenRetryingSameAttachedPage_RechecksWithoutNavigation()
    {
        var recheckCalls = 0;
        var navigateCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserNavigationTimeoutSeconds = 5
            },
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)));

        _ = await retriever.ExecuteAttachedNavigationAttemptAsync(
            attempt: 1,
            pageUrl: "https://medium.com/@author/post",
            uri: new Uri("https://medium.com/@author/post"),
            recheckAttachedPageAsync: _ =>
            {
                recheckCalls++;
                return Task.FromResult<IResponse?>(null);
            },
            navigateAttachedPageIfNeededAsync: _ =>
            {
                navigateCalls++;
                return Task.FromResult<IResponse?>(null);
            },
            cancellationToken: CancellationToken.None);

        await Assert.That(recheckCalls).IsEqualTo(1);
        await Assert.That(navigateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteAttachedNavigationAttemptAsync_WhenRetryingDifferentPage_UsesNavigationLogic()
    {
        var recheckCalls = 0;
        var navigateCalls = 0;
        var retriever = CreateRetriever(
            new MediumArticleToolOptions
            {
                BrowserNavigationTimeoutSeconds = 5
            },
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)),
            (_, _) => Task.FromResult(MediumArticleFetchResult.Failed(
                new Uri("https://medium.com/@author/post"),
                new Uri("https://medium.com/@author/post"),
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable)));

        _ = await retriever.ExecuteAttachedNavigationAttemptAsync(
            attempt: 1,
            pageUrl: "https://medium.com/",
            uri: new Uri("https://medium.com/@author/post"),
            recheckAttachedPageAsync: _ =>
            {
                recheckCalls++;
                return Task.FromResult<IResponse?>(null);
            },
            navigateAttachedPageIfNeededAsync: _ =>
            {
                navigateCalls++;
                return Task.FromResult<IResponse?>(null);
            },
            cancellationToken: CancellationToken.None);

        await Assert.That(recheckCalls).IsEqualTo(0);
        await Assert.That(navigateCalls).IsEqualTo(1);
    }

    [Test]
    public async Task CreateBrowserPageDiagnosticMessage_IncludesFinalUrlPageTitleAndMatchFlag()
    {
        var message = PlaywrightMediumArticleRetriever.CreateBrowserPageDiagnosticMessage(
            new Uri("https://medium.com/@author/post"),
            new Uri("https://search.brave.com/search?q=medium"),
            "Brave Search");

        await Assert.That(message).Contains("FinalUrl='https://search.brave.com/search?q=medium'");
        await Assert.That(message).Contains("PageTitle='Brave Search'");
        await Assert.That(message).Contains("MatchesRequestedUrl=False");
    }

    private static PlaywrightMediumArticleRetriever CreateRetriever(
        MediumArticleToolOptions options,
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchFromAttachedBrowserAsync,
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchFromLaunchedBrowserAsync,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) =>
        new(
            Options.Create(options),
            NullLogger<PlaywrightMediumArticleRetriever>.Instance,
            fetchFromAttachedBrowserAsync,
            fetchFromLaunchedBrowserAsync,
            delayAsync);
}
