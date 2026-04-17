namespace Anduril.Host.Tests;

public class StartupSetupPolicyTests
{
    [Test]
    public async Task Evaluate_NoProvidersConfigured_InContainer_SkipsInteractiveSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true);

        await Assert.That(result.RequiresSetup).IsTrue();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.ReasonMessage).IsEqualTo("No AI provider configured.");
        await Assert.That(result.SkipMessage).Contains("AI__OpenAI__ApiKey");
    }

    [Test]
    public async Task Evaluate_NoProvidersConfigured_InInteractiveSession_LaunchesSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: false,
            isUserInteractive: true,
            isInputRedirected: false);

        await Assert.That(result.RequiresSetup).IsTrue();
        await Assert.That(result.ShouldLaunchSetup).IsTrue();
        await Assert.That(result.SkipMessage).IsNull();
    }

    [Test]
    public async Task Evaluate_OllamaOnlyConfiguredAndMissing_InContainer_SkipsInteractiveSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, "llama3.1:8b", null,
            ollamaMissing: true,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true);

        await Assert.That(result.RequiresSetup).IsTrue();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.ReasonMessage)
            .IsEqualTo("AI provider configured but required dependency is missing (Ollama).");
        await Assert.That(result.SkipMessage).Contains("Ollama is configured but unavailable");
    }

    [Test]
    public async Task Evaluate_AugmentChatConfigured_DoesNotRequireSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, "augment-key", null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true);

        await Assert.That(result.RequiresSetup).IsFalse();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.ReasonMessage).IsNull();
    }

    [Test]
    public async Task Evaluate_LlamaSharpConfigured_DoesNotRequireSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, "models/model.gguf",
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true);

        await Assert.That(result.RequiresSetup).IsFalse();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.SkipMessage).IsNull();
    }

    [Test]
    public async Task Evaluate_DisabledOpenAiWithApiKey_StillRequiresSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            "openai-key", null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true,
            openAiEnabled: false);

        await Assert.That(result.RequiresSetup).IsTrue();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.ReasonMessage).IsEqualTo("No AI provider configured.");
        await Assert.That(result.SkipMessage).Contains("AI__OpenAI__Enabled");
    }

    [Test]
    public async Task Evaluate_CopilotConfigured_DoesNotRequireSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true,
            copilotApiKey: "test-copilot-placeholder",
            copilotEnabled: true);

        await Assert.That(result.RequiresSetup).IsFalse();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.SkipMessage).IsNull();
    }

    // CopilotProvider can authenticate via the local Copilot CLI daemon without an explicit API key,
    // so enabling Copilot alone must be enough to skip first-run setup.
    [Test]
    public async Task Evaluate_CopilotEnabledWithoutApiKey_DoesNotRequireSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true,
            copilotApiKey: null,
            copilotEnabled: true);

        await Assert.That(result.RequiresSetup).IsFalse();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.SkipMessage).IsNull();
    }

    [Test]
    public async Task Evaluate_CopilotEnabledWithEmptyApiKey_DoesNotRequireSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true,
            copilotApiKey: "",
            copilotEnabled: true);

        await Assert.That(result.RequiresSetup).IsFalse();
        await Assert.That(result.ShouldLaunchSetup).IsFalse();
        await Assert.That(result.SkipMessage).IsNull();
    }

    [Test]
    public async Task Evaluate_CopilotDisabled_WithApiKey_StillRequiresSetup()
    {
        var result = StartupSetupPolicy.Evaluate(
            null, null, null, null, null,
            ollamaMissing: false,
            isContainer: true,
            isUserInteractive: false,
            isInputRedirected: true,
            copilotApiKey: "test-copilot-placeholder",
            copilotEnabled: false);

        await Assert.That(result.RequiresSetup).IsTrue();
    }

    [Test]
    public async Task IsRunningInContainer_DetectsDotnetEnvironmentVariable()
    {
        bool result = StartupSetupPolicy.IsRunningInContainer(
            name => name == "DOTNET_RUNNING_IN_CONTAINER" ? "true" : null,
            _ => false);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsRunningInContainer_DetectsDockerEnvFile()
    {
        bool result = StartupSetupPolicy.IsRunningInContainer(
            _ => null,
            path => path == "/.dockerenv");

        await Assert.That(result).IsTrue();
    }
}