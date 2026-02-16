using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class AugmentChatProviderTests
{
    private static AugmentChatProvider CreateProvider(string apiKey = "", string? endpoint = null, string? model = null)
    {
        var opts = new AiProviderOptions
        {
            Provider = "augment-chat",
            ApiKey = apiKey,
            Model = model ?? ""
        };
        if (endpoint is not null) opts.Endpoint = endpoint;
        return new AugmentChatProvider(Options.Create(opts), NullLogger<AugmentChatProvider>.Instance);
    }

    [Test]
    public async Task Name_IsAugmentChat()
    {
        var provider = CreateProvider();

        await Assert.That(provider.Name).IsEqualTo("augment-chat");
    }

    [Test]
    public async Task SupportsChatCompletion_IsTrue()
    {
        var provider = CreateProvider();

        await Assert.That(provider.SupportsChatCompletion).IsTrue();
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var provider = CreateProvider();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task IsAvailable_IsFalse_WhenNoApiKey()
    {
        var provider = CreateProvider(apiKey: "");

        await provider.InitializeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task IsAvailable_IsTrue_WhenApiKeyConfigured()
    {
        var provider = CreateProvider(apiKey: "test-api-key");

        await provider.InitializeAsync();

        await Assert.That(provider.IsAvailable).IsTrue();
    }

    [Test]
    public async Task ChatClient_ThrowsBeforeInit()
    {
        var provider = CreateProvider();

        await Assert.That(() => provider.ChatClient).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ChatClient_ReturnsAfterInit()
    {
        var provider = CreateProvider(apiKey: "test-api-key");

        await provider.InitializeAsync();

        await Assert.That(provider.ChatClient).IsNotNull();
    }

    [Test]
    public async Task ChatClient_IsNotNull_AfterInitWithKey()
    {
        var provider = CreateProvider(apiKey: "test-key", model: "my-model", endpoint: "https://custom.api.com");

        await provider.InitializeAsync();

        await Assert.That(provider.ChatClient).IsNotNull();
    }

    [Test]
    public async Task GetToolsAsync_ReturnsEmpty()
    {
        var provider = CreateProvider(apiKey: "test-key");
        await provider.InitializeAsync();

        var tools = await provider.GetToolsAsync();

        await Assert.That(tools.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisposeAsync_SafeWhenNotInitialized()
    {
        var provider = CreateProvider();

        await provider.DisposeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_CleansUpClient()
    {
        var provider = CreateProvider(apiKey: "test-key");
        await provider.InitializeAsync();

        await Assert.That(provider.IsAvailable).IsTrue();

        await provider.DisposeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }
}

