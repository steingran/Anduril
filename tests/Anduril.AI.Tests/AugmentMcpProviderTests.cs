using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class AugmentMcpProviderTests
{
    private static AugmentMcpProvider CreateProvider()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "augment" });
        return new AugmentMcpProvider(options, NullLogger<AugmentMcpProvider>.Instance);
    }

    [Test]
    public async Task Name_IsAugment()
    {
        var provider = CreateProvider();

        await Assert.That(provider.Name).IsEqualTo("augment");
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var provider = CreateProvider();

        await Assert.That(provider.IsAvailable).IsFalse();
    }

    [Test]
    public async Task SupportsChatCompletion_IsFalse()
    {
        var provider = CreateProvider();

        await Assert.That(provider.SupportsChatCompletion).IsFalse();
    }

    [Test]
    public async Task ChatClient_ThrowsNotSupportedException()
    {
        var provider = CreateProvider();

        await Assert.That(() => provider.ChatClient).Throws<NotSupportedException>();
    }

    [Test]
    public async Task GetToolsAsync_ThrowsBeforeInit()
    {
        var provider = CreateProvider();

        await Assert.That(async () => await provider.GetToolsAsync()).ThrowsException()
            .WithMessageMatching("*not been initialized*");
    }

    [Test]
    public async Task DisposeAsync_SafeWhenNotInitialized()
    {
        var provider = CreateProvider();

        // Should not throw even though _mcpClient is null
        await provider.DisposeAsync();

        await Assert.That(provider.IsAvailable).IsFalse();
    }
}

