using Anduril.AI.Providers;
using Anduril.Core.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Anduril.AI.Tests;

public class ModelDefaultsTests
{
    [Test]
    public async Task OpenAiProvider_UsesGptFourO_AsDefault()
    {
        // Arrange — leave Model as null to trigger the default fallback in InitializeAsync
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "openai",
            ApiKey = "fake-key",
            Model = null!
        });
        var provider = new OpenAiProvider(options, NullLogger<OpenAiProvider>.Instance);

        // Act
        await provider.InitializeAsync();

        // Assert — provider must be available and have resolved to the expected default model
        await Assert.That(provider.IsAvailable).IsTrue();
        await Assert.That(provider.ResolvedModel).IsEqualTo("gpt-4o");
    }
}
