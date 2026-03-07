namespace Anduril.Integrations.Tests;

public class MediumArticleToolOptionsTests
{
    [Test]
    public async Task DefaultRetrievalMode_IsBrowserOnly()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.RetrievalMode).IsEqualTo(MediumArticleRetrievalMode.BrowserOnly);
    }

    [Test]
    public async Task DefaultBrowserRemoteDebuggingUrl_IsLocalChromeDebugEndpoint()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.BrowserRemoteDebuggingUrl).IsEqualTo("http://127.0.0.1:9222");
    }

    [Test]
    public async Task DefaultBrowserUserDataDirectory_IsMediumBrowserSessionPath()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.BrowserUserDataDirectory).IsEqualTo("./sessions/medium-browser");
    }

    [Test]
    public async Task DefaultBrowserManualInterventionWaitSeconds_Is120()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.BrowserManualInterventionWaitSeconds).IsEqualTo(120);
    }

    [Test]
    public async Task DefaultBrowserManualInterventionRetryCount_Is1()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.BrowserManualInterventionRetryCount).IsEqualTo(1);
    }

    [Test]
    public async Task DefaultUserAgent_IsBlank()
    {
        var options = new MediumArticleToolOptions();

        await Assert.That(options.UserAgent).IsEqualTo(string.Empty);
    }
}
