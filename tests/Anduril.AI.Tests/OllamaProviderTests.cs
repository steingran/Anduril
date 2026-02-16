using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class OllamaProviderTests
{
    [Test]
    public async Task Name_IsOllama()
    {
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "ollama",
            Model = "qwen2.5:7b",
            Endpoint = "http://localhost:11434"
        });
        var provider = new OllamaProvider(options, NullLogger<OllamaProvider>.Instance);

        await Assert.That(provider.Name).IsEqualTo("ollama");
    }

    [Test]
    public async Task IsAvailable_IsFalse_BeforeInit()
    {
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "ollama",
            Model = "qwen2.5:7b",
            Endpoint = "http://localhost:11434"
        });
        var provider = new OllamaProvider(options, NullLogger<OllamaProvider>.Instance);

        await Assert.That(provider.IsAvailable).IsFalse();
    }
}

