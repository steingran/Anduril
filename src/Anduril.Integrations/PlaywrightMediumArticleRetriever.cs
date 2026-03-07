using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Anduril.Integrations;

internal sealed class PlaywrightMediumArticleRetriever : IMediumArticleRetriever
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly ILogger<PlaywrightMediumArticleRetriever> _logger;
    private readonly MediumArticleToolOptions _options;
    private readonly Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> _fetchFromAttachedBrowserAsync;
    private readonly Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> _fetchFromLaunchedBrowserAsync;

    public PlaywrightMediumArticleRetriever(
        IOptions<MediumArticleToolOptions> options,
        ILogger<PlaywrightMediumArticleRetriever> logger)
    {
        _options = options.Value;
        _logger = logger;
        _delayAsync = (delay, cancellationToken) => Task.Delay(delay, cancellationToken);
        _fetchFromAttachedBrowserAsync = FetchFromAttachedBrowserCoreAsync;
        _fetchFromLaunchedBrowserAsync = FetchFromLaunchedBrowserCoreAsync;
    }

    internal PlaywrightMediumArticleRetriever(
        IOptions<MediumArticleToolOptions> options,
        ILogger<PlaywrightMediumArticleRetriever> logger,
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchFromAttachedBrowserAsync,
        Func<Uri, CancellationToken, Task<MediumArticleFetchResult>> fetchFromLaunchedBrowserAsync,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : this(options, logger)
    {
        _fetchFromAttachedBrowserAsync = fetchFromAttachedBrowserAsync;
        _fetchFromLaunchedBrowserAsync = fetchFromLaunchedBrowserAsync;
        _delayAsync = delayAsync ?? _delayAsync;
    }

    public async Task<MediumArticleFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        var hasAttachEndpoint = MediumArticleBrowserConfiguration.HasAttachEndpoint(_options);
        var hasLaunchProfile = MediumArticleBrowserConfiguration.HasLaunchProfile(_options);

        if (!hasAttachEndpoint && !hasLaunchProfile)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable,
                diagnosticMessage: MediumArticleBrowserConfiguration.MissingConfigurationMessage);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GetOverallTimeout());

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hasAttachEndpoint)
            {
                var attachedResult = await TryFetchFromAttachedBrowserAsync(uri, timeoutCts.Token);

                if (!ShouldFallBackToLaunchedBrowser(attachedResult) || !hasLaunchProfile)
                    return attachedResult;
            }

            return await _fetchFromLaunchedBrowserAsync(uri, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.Timeout);
        }
        catch (PlaywrightException ex)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable,
                diagnosticMessage: ex.Message);
        }
    }

    private async Task<MediumArticleFetchResult> TryFetchFromAttachedBrowserAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return await _fetchFromAttachedBrowserAsync(uri, cancellationToken);
        }
        catch (PlaywrightException ex)
        {
            return MediumArticleFetchResult.Failed(
                uri,
                uri,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.BrowserUnavailable,
                diagnosticMessage: ex.Message);
        }
    }

    private async Task<MediumArticleFetchResult> FetchFromAttachedBrowserCoreAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
        IBrowser? browser = null;

        try
        {
            browser = await playwright.Chromium.ConnectOverCDPAsync(
                _options.BrowserRemoteDebuggingUrl!,
                new BrowserTypeConnectOverCDPOptions
                {
                    Timeout = Math.Max(1, _options.BrowserNavigationTimeoutSeconds) * 1000,
                    IsLocal = true
                }).WaitAsync(cancellationToken);

            var context = browser.Contexts.FirstOrDefault();

            if (context is null)
            {
                return MediumArticleFetchResult.Failed(
                    uri,
                    uri,
                    MediumArticleRetrievalMethod.Browser,
                    MediumArticleFetchFailureReason.BrowserUnavailable,
                    diagnosticMessage: "Connected to the remote debugging endpoint, but no attachable browser context was available. Keep Chrome or Edge open with at least one regular browser window.");
            }

            var (page, closePageWhenDone) = await GetAttachedPageAsync(context, uri, cancellationToken);

            try
            {
                return await FetchWithManualInterventionAsync(
                    uri,
                    page,
                    browserIsVisible: true,
                    (attempt, ct) => ExecuteAttachedNavigationAttemptAsync(
                        attempt,
                        page.Url,
                        uri,
                        retryCancellationToken => WaitForAttachedPageAsync(page, retryCancellationToken),
                        retryCancellationToken => NavigateAttachedPageIfNeededAsync(page, uri, retryCancellationToken),
                        ct),
                    cancellationToken);
            }
            finally
            {
                if (closePageWhenDone)
                    await TryClosePageAsync(page);
            }
        }
        finally
        {
            if (browser is not null)
                await TryDisconnectBrowserAsync(browser);
        }
    }

    private async Task<MediumArticleFetchResult> FetchFromLaunchedBrowserCoreAsync(Uri uri, CancellationToken cancellationToken)
    {
        var userDataDirectory = Path.GetFullPath(_options.BrowserUserDataDirectory!);
        Directory.CreateDirectory(userDataDirectory);

        using var playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
        await using var context = await playwright.Chromium
            .LaunchPersistentContextAsync(userDataDirectory, CreateLaunchOptions())
            .WaitAsync(cancellationToken);

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().WaitAsync(cancellationToken);
        return await FetchWithManualInterventionAsync(
            uri,
            page,
            browserIsVisible: !_options.BrowserHeadless,
            (attempt, ct) => page.GotoAsync(uri.AbsoluteUri, CreateGotoOptions()).WaitAsync(ct),
            cancellationToken);
    }

    private async Task<(IPage Page, bool ClosePageWhenDone)> GetAttachedPageAsync(
        IBrowserContext context,
        Uri uri,
        CancellationToken cancellationToken)
    {
        var matchingPage = context.Pages.FirstOrDefault(page => IsSamePage(page.Url, uri));

        if (matchingPage is not null)
            return (matchingPage, false);

        var page = await context.NewPageAsync().WaitAsync(cancellationToken);
        return (page, true);
    }

    private async Task<IResponse?> NavigateAttachedPageIfNeededAsync(IPage page, Uri uri, CancellationToken cancellationToken)
    {
        if (IsSamePage(page.Url, uri))
            return await WaitForAttachedPageAsync(page, cancellationToken);

        return await page.GotoAsync(uri.AbsoluteUri, CreateGotoOptions()).WaitAsync(cancellationToken);
    }

    internal async Task<IResponse?> ExecuteAttachedNavigationAttemptAsync(
        int attempt,
        string pageUrl,
        Uri uri,
        Func<CancellationToken, Task<IResponse?>> recheckAttachedPageAsync,
        Func<CancellationToken, Task<IResponse?>> navigateAttachedPageIfNeededAsync,
        CancellationToken cancellationToken)
    {
        if (attempt > 0 && IsSamePage(pageUrl, uri))
            return await recheckAttachedPageAsync(cancellationToken);

        return await navigateAttachedPageIfNeededAsync(cancellationToken);
    }

    private async Task<MediumArticleFetchResult> CreateFetchResultAsync(
        Uri requestedUri,
        IPage page,
        IResponse? response,
        CancellationToken cancellationToken)
    {
        var html = await page.ContentAsync().WaitAsync(cancellationToken);
        var finalUrl = Uri.TryCreate(page.Url, UriKind.Absolute, out var resolvedUri)
            ? resolvedUri
            : requestedUri;
        var statusCode = response is null ? null : (HttpStatusCode?)response.Status;

        if (response is not null && !response.Ok)
        {
            var diagnosticMessage = await CreateBrowserPageDiagnosticMessageAsync(requestedUri, finalUrl, page, cancellationToken);
            return MediumArticleFetchResult.Failed(
                requestedUri,
                finalUrl,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchClassifier.ClassifyFailure((HttpStatusCode)response.Status, html),
                statusCode,
                diagnosticMessage,
                html: html);
        }

        if (MediumArticleFetchClassifier.IsCloudflareChallenge(html))
        {
            var diagnosticMessage = await CreateBrowserPageDiagnosticMessageAsync(requestedUri, finalUrl, page, cancellationToken);
            return MediumArticleFetchResult.Failed(
                requestedUri,
                finalUrl,
                MediumArticleRetrievalMethod.Browser,
                MediumArticleFetchFailureReason.CloudflareChallenge,
                statusCode,
                diagnosticMessage,
                html: html);
        }

        return MediumArticleFetchResult.Successful(
            requestedUri,
            finalUrl,
            html,
            MediumArticleRetrievalMethod.Browser,
            statusCode);
    }

    internal async Task<MediumArticleFetchResult> ExecuteManualInterventionLoopAsync(
        Uri requestedUri,
        bool browserIsVisible,
        Func<int, CancellationToken, Task<MediumArticleFetchResult>> fetchAttemptAsync,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? prepareForManualInterventionAsync = null)
    {
        var result = await fetchAttemptAsync(0, cancellationToken);

        if (!ShouldWaitForManualIntervention(result, browserIsVisible))
            return result;

        var waitDuration = TimeSpan.FromSeconds(Math.Max(1, _options.BrowserManualInterventionWaitSeconds));
        var retryCount = Math.Max(0, _options.BrowserManualInterventionRetryCount);

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            if (prepareForManualInterventionAsync is not null)
                await prepareForManualInterventionAsync(cancellationToken);

            _logger.LogWarning(
                "Medium browser retrieval for {Url} encountered {FailureReason}. Keeping the browser open for {WaitSeconds} seconds so you can complete login or any Cloudflare/browser challenge before retry {RetryAttempt} of {RetryCount}.",
                requestedUri,
                result.FailureReason,
                (int)waitDuration.TotalSeconds,
                attempt,
                retryCount);

            await _delayAsync(waitDuration, cancellationToken);
            result = await fetchAttemptAsync(attempt, cancellationToken);

            if (!ShouldWaitForManualIntervention(result, browserIsVisible) || attempt == retryCount)
                return AppendManualInterventionDiagnostic(result, attempt, waitDuration);
        }

        return result;
    }

    private PageGotoOptions CreateGotoOptions() =>
        new()
        {
            Timeout = Math.Max(1, _options.BrowserNavigationTimeoutSeconds) * 1000,
            WaitUntil = WaitUntilState.DOMContentLoaded
        };

    private BrowserTypeLaunchPersistentContextOptions CreateLaunchOptions() =>
        new()
        {
            Headless = _options.BrowserHeadless,
            Channel = NullIfWhiteSpace(_options.BrowserChannel),
            ExecutablePath = NullIfWhiteSpace(_options.BrowserExecutablePath),
            UserAgent = string.IsNullOrWhiteSpace(_options.UserAgent) ? null : _options.UserAgent,
        };

    private static bool IsSamePage(string pageUrl, Uri uri)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
            return false;

        return Uri.Compare(
            pageUri,
            uri,
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
    }

    private async Task<IResponse?> WaitForAttachedPageAsync(IPage page, CancellationToken cancellationToken)
    {
        await page.WaitForLoadStateAsync(
            LoadState.DOMContentLoaded,
            new PageWaitForLoadStateOptions
            {
                Timeout = Math.Max(1, _options.BrowserNavigationTimeoutSeconds) * 1000
            }).WaitAsync(cancellationToken);
        return null;
    }

    internal static string CreateBrowserPageDiagnosticMessage(Uri requestedUri, Uri finalUrl, string? pageTitle)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(pageTitle) ? "(empty)" : pageTitle.Trim();
        var matchesRequestedUrl = Uri.Compare(
            finalUrl,
            requestedUri,
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

        return $"Browser page diagnostics: FinalUrl='{finalUrl}'. PageTitle='{normalizedTitle}'. MatchesRequestedUrl={matchesRequestedUrl}.";
    }

    private static async Task<string> CreateBrowserPageDiagnosticMessageAsync(
        Uri requestedUri,
        Uri finalUrl,
        IPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            var pageTitle = await page.TitleAsync().WaitAsync(cancellationToken);
            return CreateBrowserPageDiagnosticMessage(requestedUri, finalUrl, pageTitle);
        }
        catch (PlaywrightException ex)
        {
            return CreateBrowserPageDiagnosticMessage(requestedUri, finalUrl, $"unavailable: {ex.Message}");
        }
    }

    private static async Task TryClosePageAsync(IPage page)
    {
        try
        {
            await page.CloseAsync();
        }
        catch (PlaywrightException)
        {
        }
    }

    private static async Task TryDisconnectBrowserAsync(IBrowser browser)
    {
        try
        {
            await browser.CloseAsync();
        }
        catch (PlaywrightException)
        {
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task<MediumArticleFetchResult> FetchWithManualInterventionAsync(
        Uri requestedUri,
        IPage page,
        bool browserIsVisible,
        Func<int, CancellationToken, Task<IResponse?>> navigateAsync,
        CancellationToken cancellationToken) =>
        await ExecuteManualInterventionLoopAsync(
            requestedUri,
            browserIsVisible,
            async (attempt, ct) =>
            {
                var response = await navigateAsync(attempt, ct);
                return await CreateFetchResultAsync(requestedUri, page, response, ct);
            },
            cancellationToken,
            ct => TryBringPageToFrontAsync(page, ct));

    private TimeSpan GetOverallTimeout()
    {
        var navigationTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.BrowserNavigationTimeoutSeconds));

        if (_options.BrowserManualInterventionRetryCount <= 0 || _options.BrowserManualInterventionWaitSeconds <= 0)
            return navigationTimeout;

        var waitDuration = TimeSpan.FromSeconds(Math.Max(1, _options.BrowserManualInterventionWaitSeconds));
        var retryCount = Math.Max(0, _options.BrowserManualInterventionRetryCount);
        return navigationTimeout + retryCount * (navigationTimeout + waitDuration);
    }

    private static MediumArticleFetchResult AppendManualInterventionDiagnostic(
        MediumArticleFetchResult result,
        int retryAttempts,
        TimeSpan waitDuration)
    {
        if (retryAttempts <= 0)
            return result;

        var retryMessage = $" Manual intervention retry was attempted {retryAttempts} time(s) after waiting {(int)waitDuration.TotalSeconds} second(s) per attempt.";
        var diagnosticMessage = string.IsNullOrWhiteSpace(result.DiagnosticMessage)
            ? retryMessage.TrimStart()
            : result.DiagnosticMessage + retryMessage;
        return result with { DiagnosticMessage = diagnosticMessage };
    }

    private static bool IsManualInterventionFailure(MediumArticleFetchResult result) =>
        !result.Success &&
        result.FailureReason is MediumArticleFetchFailureReason.AuthenticationRequired or
            MediumArticleFetchFailureReason.CloudflareChallenge or
            MediumArticleFetchFailureReason.Forbidden;

    private bool ShouldWaitForManualIntervention(MediumArticleFetchResult result, bool browserIsVisible) =>
        browserIsVisible &&
        _options.BrowserManualInterventionRetryCount > 0 &&
        _options.BrowserManualInterventionWaitSeconds > 0 &&
        IsManualInterventionFailure(result);

    private static async Task TryBringPageToFrontAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.BringToFrontAsync().WaitAsync(cancellationToken);
        }
        catch (PlaywrightException)
        {
        }
    }

    private static bool ShouldFallBackToLaunchedBrowser(MediumArticleFetchResult result) =>
        result.FailureReason == MediumArticleFetchFailureReason.BrowserUnavailable;
}
