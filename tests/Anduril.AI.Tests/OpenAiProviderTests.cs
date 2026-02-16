using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class OpenAiProviderTests
{
    [Test]
    public async Task Name_IsOpenai()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "openai", Model = "gpt-4o" });
        var provider = new OpenAiProvider(options, NullLogger<OpenAiProvider>.Instance);

        await Assert.That(provider.Name).IsEqualTo("openai");
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "openai", Model = "gpt-4o" });
        var provider = new OpenAiProvider(options, NullLogger<OpenAiProvider>.Instance);

        await Assert.That(provider.IsAvailable).IsFalse();
    }
}

