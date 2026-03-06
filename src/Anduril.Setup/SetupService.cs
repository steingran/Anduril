using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anduril.Setup;

internal sealed class SetupService
{
    public const string DefaultOllamaEndpoint = "http://localhost:11434";

    // Maps the user-facing display name to the Ollama model tag
    public static string MapOllamaModelTag(string displayName) => displayName switch
    {
        "Llama 3.1 8B (Meta)"  => "llama3.1:8b",
        "Mistral 7B (Mistral)" => "mistral",
        "GPT-OSS 20B (OpenAI)" => "GPT-OSS:20b",
        "Gemma 2 9B (Google)"  => "gemma2",
        _                      => "llama3.1:8b"
    };

    public static string NormalizeProvider(string provider)
        => provider.Trim().ToLowerInvariant() switch
        {
            "local model via ollama" => "ollama",
            "ollama" => "ollama",
            "anthropic" => "anthropic",
            "openai" => "openai",
            var normalized => normalized
        };

    public static bool IsSupportedProvider(string provider)
        => NormalizeProvider(provider) is "ollama" or "anthropic" or "openai";

    public static string GetDefaultModel(string provider)
        => NormalizeProvider(provider) switch
        {
            "ollama" => "llama3.1:8b",
            "anthropic" => "claude-3-5-sonnet-20241022",
            "openai" => "gpt-4o-mini",
            _ => string.Empty
        };

    // Resolves the appsettings.json path. Returns null when the file cannot be found.
    // explicitPath: value from command-line args (may be null or point to a non-existent file)
    // searchRoot:   starting directory for the upward directory walk (inject for testability)
    // The walk stops at a repo root (.git directory or Anduril.slnx) to avoid accidentally
    // patching config files in unrelated parent projects.
    public static string? FindConfigPath(string? explicitPath, string searchRoot)
    {
        if (explicitPath != null && File.Exists(explicitPath))
            return explicitPath;

        var start = new DirectoryInfo(searchRoot);

        // Walk up looking for the standard development layout (src/Anduril.Host or sibling Anduril.Host)
        for (var dir = start; dir != null; dir = dir.Parent)
        {
            var srcHostPath = Path.Combine(dir.FullName, "src", "Anduril.Host", "appsettings.json");
            if (File.Exists(srcHostPath))
                return srcHostPath;

            var hostPath = Path.Combine(dir.FullName, "Anduril.Host", "appsettings.json");
            if (File.Exists(hostPath))
                return hostPath;

            if (IsRepoRoot(dir))
                break;
        }

        // Fallback: walk up looking for any appsettings.json (production layout)
        for (var dir = start; dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
                return candidate;

            if (IsRepoRoot(dir))
                break;
        }

        return null;
    }

    private static bool IsRepoRoot(DirectoryInfo dir) =>
        File.Exists(Path.Combine(dir.FullName, "Anduril.slnx")) ||
        Directory.Exists(Path.Combine(dir.FullName, ".git"));

    // Reads the existing JSON, patches the AI provider section, and returns the updated JSON string.
    // Pure function — no file I/O, fully testable.
    public static string PatchConfigJson(
        string existingJson,
        string provider,
        string model,
        string apiKey,
        string endpoint)
    {
        provider = NormalizeProvider(provider);
        var root = JsonNode.Parse(existingJson)!;
        var ai = root["AI"] ??= new JsonObject();

        if (provider == "ollama")
        {
            var ollama = ai["Ollama"] ??= new JsonObject();
            ollama["Provider"] = "ollama";
            ollama["Model"] = model;
            ollama["Endpoint"] = endpoint;
        }
        else if (provider == "anthropic")
        {
            var anthropic = ai["Anthropic"] ??= new JsonObject();
            anthropic["Provider"] = "anthropic";
            anthropic["Model"] = model;
            anthropic["ApiKey"] = apiKey;
        }
        else if (provider == "openai")
        {
            var openai = ai["OpenAI"] ??= new JsonObject();
            openai["Provider"] = "openai";
            openai["Model"] = model;
            openai["ApiKey"] = apiKey;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Checks whether Ollama is installed and accessible.
    // First probes the HTTP API (covers the "already running" case), then falls back to running
    // `ollama --version` to check whether the binary is on PATH.
    public static bool IsOllamaInstalled()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var response = client.GetAsync("http://localhost:11434/api/tags").Result;
            if (response.IsSuccessStatusCode)
                return true;
        }
        catch
        {
            // Not running — fall through and check the binary
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

