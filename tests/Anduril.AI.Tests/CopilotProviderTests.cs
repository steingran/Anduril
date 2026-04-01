using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class CopilotProviderTests
{
    [Test]
    public async Task Name_IsCopilot()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await Assert.That(provider.Name).IsEqualTo("copilot");
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task SupportsChatCompletion_IsTrue()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await Assert.That(provider.SupportsChatCompletion).IsTrue();
    }

    [Test]
    public async Task ChatClient_ThrowsBeforeInit()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await Assert.That(() => provider.ChatClient).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InitializeAsync_WithoutApiKey_RemainsUnavailable()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o", ApiKey = null });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await provider.InitializeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task InitializeAsync_WithEmptyApiKey_RemainsUnavailable()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o", ApiKey = "  " });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await provider.InitializeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task GetToolsAsync_ReturnsEmptyList()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        var tools = await provider.GetToolsAsync();

        await Assert.That(tools).IsEmpty();
    }

    [Test]
    public async Task DisposeAsync_DoesNotThrow_WhenNotInitialized()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "copilot", Model = "gpt-4o" });
        var provider = new CopilotProvider(options, NullLogger<CopilotProvider>.Instance);

        await provider.DisposeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }
}
