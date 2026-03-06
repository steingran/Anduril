namespace Anduril.Setup;

internal static class SetupCliValidator
{
    public static bool TryCreateRequest(
        SetupCliOptions options,
        string searchRoot,
        out SetupRequest? request,
        out string? errorMessage)
    {
        request = null;

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            errorMessage = "Non-interactive mode requires --provider or ANDURIL_SETUP_PROVIDER.";
            return false;
        }

        var provider = SetupService.NormalizeProvider(options.Provider);
        if (!SetupService.IsSupportedProvider(provider))
        {
            errorMessage = $"Unsupported provider '{options.Provider}'. Supported values are ollama, anthropic, and openai.";
            return false;
        }

        var model = string.IsNullOrWhiteSpace(options.Model)
            ? SetupService.GetDefaultModel(provider)
            : options.Model.Trim();
        var apiKey = options.ApiKey?.Trim() ?? string.Empty;
        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? SetupService.DefaultOllamaEndpoint
            : options.Endpoint.Trim();

        if ((provider == "anthropic" || provider == "openai") && string.IsNullOrWhiteSpace(apiKey))
        {
            errorMessage = $"Provider '{provider}' requires --api-key or ANDURIL_SETUP_API_KEY in non-interactive mode.";
            return false;
        }

        var configPath = SetupService.FindConfigPath(options.ConfigPath, searchRoot) ?? "appsettings.json";
        if (!File.Exists(configPath))
        {
            errorMessage = $"Could not find 'appsettings.json'. Looked in {Path.GetFullPath(configPath)} and typical development locations.";
            return false;
        }

        request = new SetupRequest
        {
            ConfigPath = configPath,
            Provider = provider,
            Model = model,
            ApiKey = apiKey,
            Endpoint = endpoint
        };
        errorMessage = null;
        return true;
    }
}