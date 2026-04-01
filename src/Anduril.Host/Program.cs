using System.Diagnostics;
using Anduril.AI.Detection;
using Anduril.Host;
using Microsoft.Extensions.Configuration;
using Serilog;
using Velopack;

// ---------------------------------------------------------------------------
// Velopack update hooks — must run before anything else
// ---------------------------------------------------------------------------
VelopackApp.Build().Run();

// ---------------------------------------------------------------------------
// Bootstrap Serilog before anything else
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog integration
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(
            ctx.Configuration,
            SerilogConfigurationReaderOptionsFactory.Create()));

    // ------------------------------------------------------------------
    // First-run setup check (standalone host only)
    // ------------------------------------------------------------------
    var config = builder.Configuration;
    var openAiSection = config.GetSection("AI:OpenAI");
    var anthropicSection = config.GetSection("AI:Anthropic");
    var augmentChatSection = config.GetSection("AI:AugmentChat");
    var ollamaSection = config.GetSection("AI:Ollama");
    var llamaSharpSection = config.GetSection("AI:LLamaSharp");
    var copilotSection = config.GetSection("AI:Copilot");

    bool openAiEnabled = openAiSection.GetValue<bool?>("Enabled") ?? true;
    bool anthropicEnabled = anthropicSection.GetValue<bool?>("Enabled") ?? true;
    bool augmentChatEnabled = augmentChatSection.GetValue<bool?>("Enabled") ?? true;
    bool ollamaEnabled = ollamaSection.GetValue<bool?>("Enabled") ?? true;
    bool llamaSharpEnabled = llamaSharpSection.GetValue<bool?>("Enabled") ?? true;
    bool copilotEnabled = copilotSection.GetValue<bool?>("Enabled") ?? false;

    var ollamaModel = ollamaSection["Model"];
    bool ollamaMissing = false;
    if (!string.IsNullOrEmpty(ollamaModel) && ollamaEnabled)
    {
        var detector = new OllamaDetector(Microsoft.Extensions.Logging.Abstractions.NullLogger<OllamaDetector>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            if (!await detector.IsInstalledAsync(cts.Token) &&
                !await detector.IsRunningAsync(ollamaSection["Endpoint"] ?? "http://localhost:11434", cts.Token))
            {
                ollamaMissing = true;
            }
        }
        catch
        {
            // Ignore detection errors
        }
        await detector.DisposeAsync();
    }

    var (requiresSetup, shouldLaunchSetup, reasonMessage, skipMessage) = StartupSetupPolicy.Evaluate(
        openAiSection["ApiKey"],
        anthropicSection["ApiKey"],
        augmentChatSection["ApiKey"],
        ollamaModel,
        llamaSharpSection["ModelPath"],
        ollamaMissing,
        StartupSetupPolicy.IsRunningInContainer(Environment.GetEnvironmentVariable, File.Exists),
        Environment.UserInteractive,
        Console.IsInputRedirected,
        openAiEnabled: openAiEnabled,
        anthropicEnabled: anthropicEnabled,
        augmentChatEnabled: augmentChatEnabled,
        ollamaEnabled: ollamaEnabled,
        llamaSharpEnabled: llamaSharpEnabled,
        copilotApiKey: copilotSection["ApiKey"],
        copilotEnabled: copilotEnabled);

    if (requiresSetup)
    {
        if (!shouldLaunchSetup)
        {
            Log.Warning("{Message}", skipMessage);
        }
        else
        {
            Log.Information("{Message} Starting setup...", reasonMessage);

            string setupExe = OperatingSystem.IsWindows() ? "Anduril.Setup.exe" : "Anduril.Setup";
            var setupPath = Path.Combine(AppContext.BaseDirectory, setupExe);

            if (!File.Exists(setupPath))
            {
                // Fallback for development
                setupPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Anduril.Setup", "bin", "Debug", "net10.0", setupExe);
            }

            if (File.Exists(setupPath))
            {
                try
                {
                    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                    if (!File.Exists(configPath))
                    {
                        // Fallback for development: host project root
                        configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json");
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = setupPath,
                        Arguments = $"\"{configPath}\"",
                        UseShellExecute = true,
                        WorkingDirectory = AppContext.BaseDirectory
                    };
                    var process = Process.Start(psi);
                    process?.WaitForExit();

                    // Reload configuration after setup
                    if (config is IConfigurationRoot root)
                    {
                        root.Reload();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to launch setup tool at {Path}", setupPath);
                }
            }
            else
            {
                Log.Warning("Setup tool not found at {Path}", setupPath);
            }
        }
    }

    // ------------------------------------------------------------------
    // Register all Anduril services and map endpoints
    // ------------------------------------------------------------------
    builder.AddAndurilServices();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.MapAndurilEndpoints();

    Log.Information("Anduril AI Assistant starting...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Anduril terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
