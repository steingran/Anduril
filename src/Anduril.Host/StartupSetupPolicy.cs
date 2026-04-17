namespace Anduril.Host;

internal static class StartupSetupPolicy
{
    public static (bool RequiresSetup, bool ShouldLaunchSetup, string? ReasonMessage, string? SkipMessage) Evaluate(
        string? openAiApiKey,
        string? anthropicApiKey,
        string? augmentChatApiKey,
        string? ollamaModel,
        string? llamaSharpModelPath,
        bool ollamaMissing,
        bool isContainer,
        bool isUserInteractive,
        bool isInputRedirected,
        bool openAiEnabled = true,
        bool anthropicEnabled = true,
        bool augmentChatEnabled = true,
        bool ollamaEnabled = true,
        bool llamaSharpEnabled = true,
        string? copilotApiKey = null,
        bool copilotEnabled = false)
    {
        bool openAiConfigured = openAiEnabled && HasValue(openAiApiKey);
        bool anthropicConfigured = anthropicEnabled && HasValue(anthropicApiKey);
        bool augmentChatConfigured = augmentChatEnabled && HasValue(augmentChatApiKey);
        bool ollamaConfigured = ollamaEnabled && HasValue(ollamaModel);
        bool llamaSharpConfigured = llamaSharpEnabled && HasValue(llamaSharpModelPath);
        // Copilot can authenticate via the local Copilot CLI daemon without an explicit API key,
        // so treat it as configured whenever it is enabled, regardless of whether an ApiKey is set.
        bool copilotConfigured = copilotEnabled;

        bool anyAlternativeChatProviderConfigured =
            openAiConfigured || anthropicConfigured || augmentChatConfigured || llamaSharpConfigured || copilotConfigured;

        bool noChatProviderConfigured = !anyAlternativeChatProviderConfigured && !ollamaConfigured;
        bool ollamaOnlyProviderConfigured = ollamaConfigured && !anyAlternativeChatProviderConfigured;
        bool requiresSetup = noChatProviderConfigured || (ollamaOnlyProviderConfigured && ollamaMissing);

        if (!requiresSetup)
            return (false, false, null, null);

        string reasonMessage = noChatProviderConfigured
            ? "No AI provider configured."
            : "AI provider configured but required dependency is missing (Ollama).";

        if (ShouldLaunchInteractiveSetup(isContainer, isUserInteractive, isInputRedirected))
            return (true, true, reasonMessage, null);

        return (true, false, reasonMessage, BuildSkipMessage(noChatProviderConfigured, isContainer));
    }

    public static bool ShouldLaunchInteractiveSetup(bool isContainer, bool isUserInteractive, bool isInputRedirected) =>
        !isContainer && isUserInteractive && !isInputRedirected;

    public static bool IsRunningInContainer(Func<string, string?> getEnvironmentVariable, Func<string, bool> fileExists)
    {
        if (IsTrueish(getEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")) ||
            IsTrueish(getEnvironmentVariable("ASPNETCORE_RUNNING_IN_CONTAINER")))
        {
            return true;
        }

        return fileExists("/.dockerenv");
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsTrueish(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.Ordinal);

    private static string BuildSkipMessage(bool noChatProviderConfigured, bool isContainer)
    {
        string environmentDescription = isContainer ? "a container" : "a non-interactive environment";
        const string configurationHelp = "Configure AI settings via appsettings.json or environment variables before starting. Supported settings include AI__OpenAI__Enabled / AI__OpenAI__ApiKey, AI__Anthropic__Enabled / AI__Anthropic__ApiKey, AI__AugmentChat__Enabled / AI__AugmentChat__ApiKey, AI__Copilot__Enabled / AI__Copilot__ApiKey, AI__Ollama__Enabled / AI__Ollama__Model / AI__Ollama__Endpoint, and AI__LLamaSharp__Enabled / AI__LLamaSharp__ModelPath.";

        return noChatProviderConfigured
            ? $"No AI provider is configured, but interactive first-run setup was skipped because the host is running in {environmentDescription}. {configurationHelp}"
            : $"Ollama is configured but unavailable, and interactive first-run setup was skipped because the host is running in {environmentDescription}. Configure a different AI provider or ensure Ollama is installed and reachable. {configurationHelp}";
    }
}