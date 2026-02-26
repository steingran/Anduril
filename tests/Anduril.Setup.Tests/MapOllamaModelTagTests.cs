namespace Anduril.Setup.Tests;

public class MapOllamaModelTagTests
{
    [Test]
    [Arguments("Llama 3.1 8B (Meta)", "llama3.1:8b")]
    [Arguments("Mistral 7B (Mistral)", "mistral")]
    [Arguments("GPT-OSS 20B (OpenAI)", "GPT-OSS:20b")]
    [Arguments("Gemma 2 9B (Google)", "gemma2")]
    public async Task MapOllamaModelTag_KnownDisplayName_ReturnsExpectedTag(string displayName, string expectedTag)
    {
        var result = SetupService.MapOllamaModelTag(displayName);
        await Assert.That(result).IsEqualTo(expectedTag);
    }

    [Test]
    [Arguments("unknown model")]
    [Arguments("")]
    [Arguments("Some Random Model XYZ")]
    public async Task MapOllamaModelTag_UnknownDisplayName_ReturnsDefaultLlamaTag(string displayName)
    {
        var result = SetupService.MapOllamaModelTag(displayName);
        await Assert.That(result).IsEqualTo("llama3.1:8b");
    }
}

