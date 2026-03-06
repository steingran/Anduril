using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;

namespace Anduril.Setup;

internal static class Program
{
    private const string UsageText = """
        Usage:
          Anduril.Setup [config-path]
          Anduril.Setup --help
          Anduril.Setup --non-interactive --provider <ollama|anthropic|openai> [options]

        Options:
          --config, --config-path <path>   Path to appsettings.json
          --provider <name>                openai, anthropic, or ollama
          --model <name>                   Optional; provider default is used when omitted
          --api-key <key>                  Required for OpenAI and Anthropic
          --endpoint <url>                 Optional; defaults to http://localhost:11434 for Ollama

        Environment variables:
          ANDURIL_SETUP_NON_INTERACTIVE
          ANDURIL_SETUP_PROVIDER
          ANDURIL_SETUP_MODEL
          ANDURIL_SETUP_API_KEY
          ANDURIL_SETUP_ENDPOINT
          ANDURIL_SETUP_CONFIG_PATH

        Command-line arguments override environment variables.
        """;

    public static int Main(string[] args)
    {
        if (!SetupCliParser.TryParse(args, Environment.GetEnvironmentVariable, out var options, out var errorMessage))
        {
            WriteError(errorMessage ?? "Failed to parse command-line arguments.");
            WriteUsage();
            return 1;
        }

        if (options is null)
        {
            WriteError("Failed to parse command-line arguments.");
            return 1;
        }

        if (options.ShowHelp)
        {
            WriteUsage();
            return 0;
        }

        return options.NonInteractive
            ? RunNonInteractive(options)
            : RunInteractive(options.ConfigPath);
    }

    private static int RunNonInteractive(SetupCliOptions options)
    {
        if (!SetupCliValidator.TryCreateRequest(options, Directory.GetCurrentDirectory(), out var request, out var errorMessage))
        {
            WriteError(errorMessage ?? "Invalid non-interactive setup options.");
            WriteUsage();
            return 1;
        }

        try
        {
            ApplyConfiguration(request!.ConfigPath, request.Provider, request.Model, request.ApiKey, request.Endpoint);
            AnsiConsole.MarkupLine($"[green]Configuration saved successfully to {Markup.Escape(request.ConfigPath)}.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            WriteError($"Error saving configuration: {ex.Message}");
            return 1;
        }
    }

    private static int RunInteractive(string? explicitConfigPath)
    {
        WriteBanner();

        var providerDisplay = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which [green]AI provider[/] would you like to use?")
                .PageSize(10)
                .AddChoices(["Local model via Ollama", "Anthropic", "OpenAI"]));

        var provider = SetupService.NormalizeProvider(providerDisplay);
        var model = string.Empty;
        var apiKey = string.Empty;
        var endpoint = SetupService.DefaultOllamaEndpoint;
        var setupSucceeded = true;

        if (provider == "ollama")
        {
            model = SetupService.MapOllamaModelTag(AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a [green]local model[/]:")
                    .PageSize(10)
                    .AddChoices([
                        "Llama 3.1 8B (Meta)",
                        "Mistral 7B (Mistral)",
                        "GPT-OSS 20B (OpenAI)",
                        "Gemma 2 9B (Google)"
                    ])));

            var runtime = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("How would you like to run Ollama?")
                    .AddChoices(["Native executable", "Docker"]));

            if (runtime == "Native executable" &&
                !SetupService.IsOllamaInstalled() &&
                AnsiConsole.Confirm("Ollama is not installed. Would you like to install it now?", defaultValue: false))
            {
                InstallOllama();
            }

            AnsiConsole.Status().Start("Ensuring Ollama is running and model is pulled...", ctx =>
            {
                try
                {
                    using var client = new HttpClient();
                    var running = false;

                    try
                    {
                        var response = client.GetAsync($"{endpoint}/api/tags").Result;
                        running = response.IsSuccessStatusCode;
                    }
                    catch
                    {
                        // Not running.
                    }

                    if (!running)
                    {
                        StartOllama(runtime, ctx);

                        for (var i = 0; i < 15; i++)
                        {
                            Thread.Sleep(2000);
                            try
                            {
                                var response = client.GetAsync($"{endpoint}/api/tags").Result;
                                if (response.IsSuccessStatusCode)
                                {
                                    running = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // Keep polling.
                            }
                        }
                    }

                    if (!running)
                    {
                        throw new Exception("Ollama is not running and could not be started automatically. Please ensure it is installed and running.");
                    }

                    ctx.Status($"Pulling model [green]{model}[/]... This may take a while.");
                    var pullResponse = client.PostAsync(
                        $"{endpoint}/api/pull",
                        new StringContent(JsonSerializer.Serialize(new { name = model }))).Result;

                    if (!pullResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to pull model {model}.");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"Error: {ex.Message}");
                    setupSucceeded = false;
                }
            });
        }
        else if (provider == "anthropic")
        {
            apiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]Anthropic API Key[/]:")
                    .Secret()
                    .Validate(v => !string.IsNullOrWhiteSpace(v), "API key cannot be empty."));
            model = SetupService.GetDefaultModel(provider);
        }
        else if (provider == "openai")
        {
            apiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]OpenAI API Key[/]:")
                    .Secret()
                    .Validate(v => !string.IsNullOrWhiteSpace(v), "API key cannot be empty."));
            model = SetupService.GetDefaultModel(provider);
        }

        if (setupSucceeded)
        {
            AnsiConsole.Status().Start("Saving configuration...", _ =>
            {
                try
                {
                    ApplyConfiguration(ResolveConfigPath(explicitConfigPath), provider, model, apiKey, endpoint);
                    AnsiConsole.MarkupLine("[green]Configuration saved successfully![/]");
                }
                catch (Exception ex)
                {
                    WriteError($"Error saving configuration: {ex.Message}");
                    setupSucceeded = false;
                }
            });
        }

        AnsiConsole.MarkupLine(setupSucceeded
            ? "\n[bold green]Setup complete![/] You can now start Anduril."
            : "\n[bold red]Setup failed![/] Please fix the errors above and try again.");

        if (ShouldPauseBeforeExit())
        {
            AnsiConsole.MarkupLine("Press any key to exit...");
            Console.ReadKey();
        }

        return setupSucceeded ? 0 : 1;
    }

    private static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("Anduril").Color(Color.Green));
        AnsiConsole.MarkupLine("[bold]Welcome to Anduril AI Assistant Setup![/]");
        AnsiConsole.WriteLine();
    }

    private static void StartOllama(string runtime, StatusContext ctx)
    {
        if (runtime == "Native executable")
        {
            ctx.Status("Ollama is not running. Attempting to start [green]ollama serve[/]...");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch
            {
                // Ignore and fall through to retry loop.
            }

            return;
        }

        ctx.Status("Ollama is not running in Docker. Attempting to start container...");
        try
        {
            var startProc = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "start ollama",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            startProc?.WaitForExit();

            if (startProc is null || startProc.ExitCode != 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }
        catch
        {
            // Ignore and fall through to retry loop.
        }
    }

    private static string ResolveConfigPath(string? explicitConfigPath)
        => SetupService.FindConfigPath(explicitConfigPath, Directory.GetCurrentDirectory()) ?? "appsettings.json";

    private static void ApplyConfiguration(string configPath, string provider, string model, string apiKey, string endpoint)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Could not find 'appsettings.json'. Looked in {Path.GetFullPath(configPath)} and typical development locations.");
        }

        var json = File.ReadAllText(configPath);
        var patched = SetupService.PatchConfigJson(json, provider, model, apiKey, endpoint);
        File.WriteAllText(configPath, patched);
    }

    private static bool ShouldPauseBeforeExit() => Environment.UserInteractive && !Console.IsInputRedirected;

    private static void WriteUsage() => Console.WriteLine(UsageText);

    private static void WriteError(string message)
        => AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");

    private static void InstallOllama()
    {
        if (OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine("Opening [green]ollama.com[/] to download the Windows installer...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ollama.com/download/windows",
                UseShellExecute = true
            });
            AnsiConsole.MarkupLine("Please complete the installation and then return here.");
            AnsiConsole.Markup("Press [green]Enter[/] once Ollama is installed...");
            Console.ReadLine();
            AnsiConsole.WriteLine();
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            AnsiConsole.Status().Start("Installing Ollama via [green]Homebrew[/]...", ctx =>
            {
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "brew",
                        Arguments = "install ollama",
                        UseShellExecute = false
                    });
                    process?.WaitForExit();

                    ctx.Status("Starting Ollama service...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "brew",
                        Arguments = "services start ollama",
                        UseShellExecute = false
                    });
                }
                catch (Exception ex)
                {
                    WriteError($"Error installing Ollama: {ex.Message}");
                }
            });
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            AnsiConsole.Status().Start("Installing Ollama via [green]install script[/]...", _ =>
            {
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "sh",
                        Arguments = "-c \"curl -fsSL https://ollama.com/install.sh | sh\"",
                        UseShellExecute = false
                    });
                    process?.WaitForExit();
                }
                catch (Exception ex)
                {
                    WriteError($"Error installing Ollama: {ex.Message}");
                }
            });
        }
    }
}
