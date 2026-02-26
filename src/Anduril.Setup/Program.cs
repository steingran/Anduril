using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;
using Anduril.Setup;

AnsiConsole.Write(
    new FigletText("Anduril")
        .Color(Color.Green));

AnsiConsole.MarkupLine("[bold]Welcome to Anduril AI Assistant Setup![/]");
AnsiConsole.WriteLine();

var providerDisplay = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Which [green]AI provider[/] would you like to use?")
        .PageSize(10)
        .AddChoices(new[] {
            "Local model via Ollama",
            "Anthropic",
            "OpenAI"
        }));

// Map the user-facing display name to a stable provider ID used in config patching
var provider = providerDisplay switch
{
    "Local model via Ollama" => "ollama",
    "Anthropic"              => "anthropic",
    "OpenAI"                 => "openai",
    _                        => providerDisplay.ToLowerInvariant()
};

string model = "";
string apiKey = "";
string endpoint = "http://localhost:11434";
bool setupSucceeded = true;

if (provider == "ollama")
{
    model = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select a [green]local model[/]:")
            .PageSize(10)
            .AddChoices(new[] {
                "Llama 3.1 8B (Meta)",
                "Mistral 7B (Mistral)",
                "GPT-OSS 20B (OpenAI)",
                "Gemma 2 9B (Google)"
            }));

    // Map to Ollama tags
    model = SetupService.MapOllamaModelTag(model);

    var runtime = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("How would you like to run Ollama?")
            .AddChoices(new[] { "Native executable", "Docker" }));

    if (runtime == "Native executable")
    {
        if (!SetupService.IsOllamaInstalled() && AnsiConsole.Confirm("Ollama is not installed. Would you like to install it now?", defaultValue: false))
        {
            InstallOllama();
        }
    }

    AnsiConsole.Status()
        .Start("Ensuring Ollama is running and model is pulled...", ctx =>
        {
            try
            {
                using var client = new HttpClient();
                bool running = false;
                try
                {
                    var response = client.GetAsync($"{endpoint}/api/tags").Result;
                    running = response.IsSuccessStatusCode;
                }
                catch
                {
                    // Not running
                }

                if (!running)
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
                        catch { }
                    }
                    else // Docker
                    {
                        ctx.Status("Ollama is not running in Docker. Attempting to start container...");
                        try
                        {
                            // Try docker start first
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
                                // Try docker run
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "docker",
                                    Arguments = "run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                });
                            }
                        }
                        catch { }
                    }

                    // Poll for a few seconds
                    for (int i = 0; i < 15; i++)
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
                        catch { }
                    }
                }

                if (!running)
                {
                    throw new Exception("Ollama is not running and could not be started automatically. Please ensure it is installed and running.");
                }

                ctx.Status($"Pulling model [green]{model}[/]... This may take a while.");
                var pullResponse = client.PostAsync($"{endpoint}/api/pull",
                    new StringContent(JsonSerializer.Serialize(new { name = model }))).Result;

                if (!pullResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to pull model {model}.");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
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
    model = "claude-3-5-sonnet-20241022";
}
else if (provider == "openai")
{
    apiKey = AnsiConsole.Prompt(
        new TextPrompt<string>("Enter your [green]OpenAI API Key[/]:")
            .Secret()
            .Validate(v => !string.IsNullOrWhiteSpace(v), "API key cannot be empty."));
    model = "gpt-4o-mini";
}

// Save to appsettings.json
if (setupSucceeded)
{
    AnsiConsole.Status()
        .Start("Saving configuration...", ctx =>
        {
            try
            {
                string configPath =
                    SetupService.FindConfigPath(args.Length > 0 ? args[0] : null, Directory.GetCurrentDirectory())
                    ?? "appsettings.json";

                if (!File.Exists(configPath))
                    throw new FileNotFoundException($"Could not find 'appsettings.json'. Looked in {Path.GetFullPath(configPath)} and typical development locations.");

                string json = File.ReadAllText(configPath);
                string patched = SetupService.PatchConfigJson(json, provider, model, apiKey, endpoint);
                File.WriteAllText(configPath, patched);
                AnsiConsole.MarkupLine("[green]Configuration saved successfully![/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error saving configuration:[/] {ex.Message}");
                setupSucceeded = false;
            }
        });
}

if (setupSucceeded)
{
    AnsiConsole.MarkupLine("\n[bold green]Setup complete![/] You can now start Anduril.");
}
else
{
    AnsiConsole.MarkupLine("\n[bold red]Setup failed![/] Please fix the errors above and try again.");
}

AnsiConsole.MarkupLine("Press any key to exit...");
Console.ReadKey();

static void InstallOllama()
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
    }
    else if (OperatingSystem.IsMacOS())
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
                AnsiConsole.MarkupLine($"[red]Error installing Ollama:[/] {ex.Message}");
            }
        });
    }
    else if (OperatingSystem.IsLinux())
    {
        AnsiConsole.Status().Start("Installing Ollama via [green]install script[/]...", ctx =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = "-c \"curl -fsSL https://ollama.com/install.sh | sh\"",
                    UseShellExecute = false
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error installing Ollama:[/] {ex.Message}");
            }
        });
    }
}
