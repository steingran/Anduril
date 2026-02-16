using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class AnthropicProviderTests
{
    [Test]
    public async Task Name_IsAnthropic()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "anthropic", Model = "claude-sonnet-4-20250514" });
        var provider = new AnthropicProvider(options, NullLogger<AnthropicProvider>.Instance);

        await Assert.That(provider.Name).IsEqualTo("anthropic");
    }
}

