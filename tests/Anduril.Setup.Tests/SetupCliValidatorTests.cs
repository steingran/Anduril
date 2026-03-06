namespace Anduril.Setup.Tests;

public class SetupCliValidatorTests
{
    private static string MakeTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task TryCreateRequest_OpenAiWithoutModel_UsesDefaultModel()
    {
        var root = MakeTempRoot();
        try
        {
            var configPath = Path.Combine(root, "appsettings.json");
            File.WriteAllText(configPath, "{}");

            var success = SetupCliValidator.TryCreateRequest(
                new SetupCliOptions { NonInteractive = true, Provider = "openai", ApiKey = "sk-test", ConfigPath = configPath },
                root,
                out var request,
                out var errorMessage);

            await Assert.That(success).IsTrue();
            await Assert.That(errorMessage).IsNull();
            await Assert.That(request).IsNotNull();
            await Assert.That(request!.Model).IsEqualTo(SetupService.GetDefaultModel("openai"));
            await Assert.That(request.Endpoint).IsEqualTo(SetupService.DefaultOllamaEndpoint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TryCreateRequest_OllamaDisplayName_UsesNormalizedProviderAndDefaults()
    {
        var root = MakeTempRoot();
        try
        {
            var configPath = Path.Combine(root, "src", "Anduril.Host", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, "{}");

            var success = SetupCliValidator.TryCreateRequest(
                new SetupCliOptions { NonInteractive = true, Provider = "Local model via Ollama" },
                root,
                out var request,
                out var errorMessage);

            await Assert.That(success).IsTrue();
            await Assert.That(errorMessage).IsNull();
            await Assert.That(request).IsNotNull();
            await Assert.That(request!.Provider).IsEqualTo("ollama");
            await Assert.That(request.Model).IsEqualTo(SetupService.GetDefaultModel("ollama"));
            await Assert.That(request.Endpoint).IsEqualTo(SetupService.DefaultOllamaEndpoint);
            await Assert.That(request.ConfigPath).IsEqualTo(configPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TryCreateRequest_MissingProvider_Fails()
    {
        var success = SetupCliValidator.TryCreateRequest(
            new SetupCliOptions { NonInteractive = true },
            Directory.GetCurrentDirectory(),
            out var request,
            out var errorMessage);

        await Assert.That(success).IsFalse();
        await Assert.That(request).IsNull();
        await Assert.That(errorMessage).Contains("requires --provider");
    }

    [Test]
    public async Task TryCreateRequest_AnthropicWithoutApiKey_Fails()
    {
        var root = MakeTempRoot();
        try
        {
            var configPath = Path.Combine(root, "appsettings.json");
            File.WriteAllText(configPath, "{}");

            var success = SetupCliValidator.TryCreateRequest(
                new SetupCliOptions { NonInteractive = true, Provider = "anthropic", ConfigPath = configPath },
                root,
                out var request,
                out var errorMessage);

            await Assert.That(success).IsFalse();
            await Assert.That(request).IsNull();
            await Assert.That(errorMessage).Contains("requires --api-key");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}