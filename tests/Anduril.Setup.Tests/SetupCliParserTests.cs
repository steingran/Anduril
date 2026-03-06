namespace Anduril.Setup.Tests;

public class SetupCliParserTests
{
    [Test]
    public async Task TryParse_CommandLineArguments_ParsesExpectedOptions()
    {
        var success = SetupCliParser.TryParse(
            ["--non-interactive", "--provider", "openai", "--api-key", "sk-test", "--config", "custom.json"],
            _ => null,
            out var options,
            out var errorMessage);

        await Assert.That(success).IsTrue();
        await Assert.That(errorMessage).IsNull();
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.NonInteractive).IsTrue();
        await Assert.That(options.Provider).IsEqualTo("openai");
        await Assert.That(options.ApiKey).IsEqualTo("sk-test");
        await Assert.That(options.ConfigPath).IsEqualTo("custom.json");
    }

    [Test]
    public async Task TryParse_EnvironmentVariables_AreUsedWhenArgumentsAreMissing()
    {
        var env = new Dictionary<string, string>
        {
            ["ANDURIL_SETUP_NON_INTERACTIVE"] = "true",
            ["ANDURIL_SETUP_PROVIDER"] = "anthropic",
            ["ANDURIL_SETUP_API_KEY"] = "sk-env",
            ["ANDURIL_SETUP_MODEL"] = "claude-custom",
            ["ANDURIL_SETUP_CONFIG_PATH"] = "env.json"
        };

        var success = SetupCliParser.TryParse(
            [],
            name => env.TryGetValue(name, out var value) ? value : null,
            out var options,
            out var errorMessage);

        await Assert.That(success).IsTrue();
        await Assert.That(errorMessage).IsNull();
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.NonInteractive).IsTrue();
        await Assert.That(options.Provider).IsEqualTo("anthropic");
        await Assert.That(options.ApiKey).IsEqualTo("sk-env");
        await Assert.That(options.Model).IsEqualTo("claude-custom");
        await Assert.That(options.ConfigPath).IsEqualTo("env.json");
    }

    [Test]
    public async Task TryParse_CommandLineArguments_OverrideEnvironmentVariables()
    {
        var env = new Dictionary<string, string>
        {
            ["ANDURIL_SETUP_NON_INTERACTIVE"] = "true",
            ["ANDURIL_SETUP_PROVIDER"] = "openai",
            ["ANDURIL_SETUP_API_KEY"] = "sk-env"
        };

        var success = SetupCliParser.TryParse(
            ["--provider", "anthropic", "--api-key", "sk-cli"],
            name => env.TryGetValue(name, out var value) ? value : null,
            out var options,
            out var errorMessage);

        await Assert.That(success).IsTrue();
        await Assert.That(errorMessage).IsNull();
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.Provider).IsEqualTo("anthropic");
        await Assert.That(options.ApiKey).IsEqualTo("sk-cli");
    }

    [Test]
    public async Task TryParse_PositionalConfigPath_IsAccepted()
    {
        var success = SetupCliParser.TryParse(
            ["appsettings.custom.json"],
            _ => null,
            out var options,
            out var errorMessage);

        await Assert.That(success).IsTrue();
        await Assert.That(errorMessage).IsNull();
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.ConfigPath).IsEqualTo("appsettings.custom.json");
    }

    [Test]
    public async Task TryParse_UnknownOption_ReturnsError()
    {
        var success = SetupCliParser.TryParse(
            ["--bogus"],
            _ => null,
            out var options,
            out var errorMessage);

        await Assert.That(success).IsFalse();
        await Assert.That(options).IsNull();
        await Assert.That(errorMessage).Contains("Unknown option");
    }
}