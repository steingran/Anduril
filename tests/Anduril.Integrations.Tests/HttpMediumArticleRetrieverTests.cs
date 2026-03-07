using System.Net;
using Microsoft.Extensions.Options;

namespace Anduril.Integrations.Tests;

public class HttpMediumArticleRetrieverTests
{
    [Test]
    public async Task CreateRequestMessage_WhenUserAgentIsBlank_DoesNotAddUserAgentHeader()
    {
        var retriever = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK));

        using var request = retriever.CreateRequestMessage(new Uri("https://medium.com/@author/post"));
        var hasUserAgent = request.Headers.TryGetValues("User-Agent", out _);

        await Assert.That(hasUserAgent).IsFalse();
    }

    [Test]
    public async Task CreateRequestMessage_WithCustomUserAgent_AddsUserAgentHeader()
    {
        var retriever = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK), userAgent: "Anduril.Tests/1.0");

        using var request = retriever.CreateRequestMessage(new Uri("https://medium.com/@author/post"));
        var hasUserAgent = request.Headers.TryGetValues("User-Agent", out var values);

        await Assert.That(hasUserAgent).IsTrue();
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Single()).IsEqualTo("Anduril.Tests/1.0");
    }

    [Test]
    public async Task FetchAsync_WhenResponseIsSuccessful_ReturnsSuccessResult()
    {
        var retriever = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body><article>Hello</article></body></html>")
        });

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.RetrievalMethod).IsEqualTo(MediumArticleRetrievalMethod.Http);
        await Assert.That(result.Html).Contains("<article>Hello</article>");
    }

    [Test]
    public async Task FetchAsync_WhenResponseContainsCloudflareChallenge_ClassifiesChallenge()
    {
        var retriever = CreateRetriever(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("<html><body>Enable JavaScript and cookies to continue</body></html>")
        });

        var result = await retriever.FetchAsync(new Uri("https://awstip.com/post"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.CloudflareChallenge);
        await Assert.That((int?)result.StatusCode).IsEqualTo((int)HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task FetchAsync_WhenRequestTimesOut_ReturnsTimeoutFailure()
    {
        var retriever = CreateRetriever(_ => throw new OperationCanceledException());

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.Timeout);
    }

    [Test]
    public async Task FetchAsync_WhenSendThrowsHttpRequestException_ReturnsNetworkFailure()
    {
        var retriever = CreateRetriever(_ => throw new HttpRequestException("boom"));

        var result = await retriever.FetchAsync(new Uri("https://medium.com/@author/post"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureReason).IsEqualTo(MediumArticleFetchFailureReason.NetworkError);
        await Assert.That(result.DiagnosticMessage).Contains("boom");
    }

    private static HttpMediumArticleRetriever CreateRetriever(
        Func<HttpRequestMessage, HttpResponseMessage> send,
        string userAgent = "") =>
        new(
            Options.Create(new MediumArticleToolOptions
            {
                RequestTimeoutSeconds = 20,
                UserAgent = userAgent,
            }),
            new SingleClientFactory(new StubHttpMessageHandler((request, _) => Task.FromResult(send(request)))));

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _httpClient = new(handler, disposeHandler: true);

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
