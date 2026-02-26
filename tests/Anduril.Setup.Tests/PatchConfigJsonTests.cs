using System.Text.Json.Nodes;

namespace Anduril.Setup.Tests;

public class PatchConfigJsonTests
{
    private const string EmptyJson = "{}";

    // Helper: parse result and navigate the JSON tree
    private static JsonNode? GetNode(string json, params string[] path)
    {
        var node = JsonNode.Parse(json);
        foreach (var key in path)
            node = node?[key];
        return node;
    }

    // --- Ollama ---

    [Test]
    public async Task PatchConfigJson_OllamaProvider_WritesProviderModelAndEndpoint()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "ollama", "llama3.1:8b", "", "http://localhost:11434");

        await Assert.That(GetNode(result, "AI", "Ollama", "Provider")!.GetValue<string>()).IsEqualTo("ollama");
        await Assert.That(GetNode(result, "AI", "Ollama", "Model")!.GetValue<string>()).IsEqualTo("llama3.1:8b");
        await Assert.That(GetNode(result, "AI", "Ollama", "Endpoint")!.GetValue<string>()).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task PatchConfigJson_OllamaProvider_DoesNotWriteApiKey()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "ollama", "llama3.1:8b", "should-be-ignored", "http://localhost:11434");

        await Assert.That(GetNode(result, "AI", "Ollama", "ApiKey")).IsNull();
    }

    // --- Anthropic ---

    [Test]
    public async Task PatchConfigJson_AnthropicProvider_WritesProviderModelAndApiKey()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "anthropic", "claude-3-5-sonnet-20241022", "sk-ant-key", "");

        await Assert.That(GetNode(result, "AI", "Anthropic", "Provider")!.GetValue<string>()).IsEqualTo("anthropic");
        await Assert.That(GetNode(result, "AI", "Anthropic", "Model")!.GetValue<string>()).IsEqualTo("claude-3-5-sonnet-20241022");
        await Assert.That(GetNode(result, "AI", "Anthropic", "ApiKey")!.GetValue<string>()).IsEqualTo("sk-ant-key");
    }

    [Test]
    public async Task PatchConfigJson_AnthropicProvider_DoesNotWriteEndpoint()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "anthropic", "claude-3-5-sonnet-20241022", "sk-ant-key", "should-be-ignored");

        await Assert.That(GetNode(result, "AI", "Anthropic", "Endpoint")).IsNull();
    }

    // --- OpenAI ---

    [Test]
    public async Task PatchConfigJson_OpenAIProvider_WritesProviderModelAndApiKey()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "openai", "gpt-4o", "sk-openai-key", "");

        await Assert.That(GetNode(result, "AI", "OpenAI", "Provider")!.GetValue<string>()).IsEqualTo("openai");
        await Assert.That(GetNode(result, "AI", "OpenAI", "Model")!.GetValue<string>()).IsEqualTo("gpt-4o");
        await Assert.That(GetNode(result, "AI", "OpenAI", "ApiKey")!.GetValue<string>()).IsEqualTo("sk-openai-key");
    }

    // --- Unknown provider ---

    [Test]
    public async Task PatchConfigJson_UnknownProvider_CreatesAISectionWithoutProviderNode()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "SomeUnknownProvider", "model", "key", "endpoint");

        // AI section must exist
        await Assert.That(GetNode(result, "AI")).IsNotNull();
        // But no provider-specific sub-node should be written
        await Assert.That(GetNode(result, "AI", "SomeUnknownProvider")).IsNull();
        await Assert.That(GetNode(result, "AI", "Ollama")).IsNull();
        await Assert.That(GetNode(result, "AI", "Anthropic")).IsNull();
        await Assert.That(GetNode(result, "AI", "OpenAI")).IsNull();
    }

    // --- Preservation of existing properties ---

    [Test]
    public async Task PatchConfigJson_PreservesExistingTopLevelProperties()
    {
        var existingJson = """{"Logging":{"LogLevel":{"Default":"Information"}},"ConnectionStrings":{"Db":"server=localhost"}}""";

        var result = SetupService.PatchConfigJson(existingJson,
            "anthropic", "claude-3-5-sonnet-20241022", "sk-ant-key", "");

        await Assert.That(GetNode(result, "Logging", "LogLevel", "Default")!.GetValue<string>()).IsEqualTo("Information");
        await Assert.That(GetNode(result, "ConnectionStrings", "Db")!.GetValue<string>()).IsEqualTo("server=localhost");
    }

    // --- Idempotency ---

    [Test]
    public async Task PatchConfigJson_CalledTwiceWithSameValues_ProducesSameResult()
    {
        var first = SetupService.PatchConfigJson(EmptyJson,
            "openai", "gpt-4o", "sk-key", "");
        var second = SetupService.PatchConfigJson(first,
            "openai", "gpt-4o", "sk-key", "");

        await Assert.That(GetNode(second, "AI", "OpenAI", "Model")!.GetValue<string>()).IsEqualTo("gpt-4o");
        await Assert.That(GetNode(second, "AI", "OpenAI", "ApiKey")!.GetValue<string>()).IsEqualTo("sk-key");
    }

    [Test]
    public async Task PatchConfigJson_OverwritesExistingProviderValues()
    {
        var initial = SetupService.PatchConfigJson(EmptyJson,
            "openai", "gpt-4o", "old-key", "");
        var updated = SetupService.PatchConfigJson(initial,
            "openai", "gpt-4-turbo", "new-key", "");

        await Assert.That(GetNode(updated, "AI", "OpenAI", "Model")!.GetValue<string>()).IsEqualTo("gpt-4-turbo");
        await Assert.That(GetNode(updated, "AI", "OpenAI", "ApiKey")!.GetValue<string>()).IsEqualTo("new-key");
    }

    // --- AI section creation ---

    [Test]
    public async Task PatchConfigJson_CreatesAISectionIfMissing()
    {
        var result = SetupService.PatchConfigJson(EmptyJson,
            "anthropic", "claude-3-5-sonnet-20241022", "sk-ant-key", "");

        await Assert.That(GetNode(result, "AI")).IsNotNull();
    }
}

