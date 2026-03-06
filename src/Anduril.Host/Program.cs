using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anduril.AI.Detection;
using Anduril.AI.Providers;
using Anduril.Communication;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Core.Webhooks;
using Anduril.Host;
using Anduril.Host.Services;
using Anduril.Integrations;
using Anduril.Skills;
using Anduril.Skills.Compiled;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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
        .ReadFrom.Configuration(ctx.Configuration));

    var config = builder.Configuration;
    var openAiSection = config.GetSection("AI:OpenAI");
    var anthropicSection = config.GetSection("AI:Anthropic");
    var augmentSection = config.GetSection("AI:Augment");
    var augmentChatSection = config.GetSection("AI:AugmentChat");
    var ollamaSection = config.GetSection("AI:Ollama");
    var llamaSharpSection = config.GetSection("AI:LLamaSharp");
    var cliSection = config.GetSection("Communication:Cli");
    var slackSection = config.GetSection("Communication:Slack");
    var teamsSection = config.GetSection("Communication:Teams");
    var signalSection = config.GetSection("Communication:Signal");

    bool openAiEnabled = openAiSection.GetValue<bool?>("Enabled") ?? true;
    bool anthropicEnabled = anthropicSection.GetValue<bool?>("Enabled") ?? true;
    bool augmentEnabled = augmentSection.GetValue<bool?>("Enabled") ?? true;
    bool augmentChatEnabled = augmentChatSection.GetValue<bool?>("Enabled") ?? true;
    bool ollamaEnabled = ollamaSection.GetValue<bool?>("Enabled") ?? true;
    bool llamaSharpEnabled = llamaSharpSection.GetValue<bool?>("Enabled") ?? true;
    bool cliEnabled = cliSection.GetValue<bool?>("Enabled") ?? true;
    bool slackEnabled = slackSection.GetValue<bool?>("Enabled") ?? true;
    bool teamsEnabled = teamsSection.GetValue<bool?>("Enabled") ?? true;
    bool signalEnabled = signalSection.GetValue<bool?>("Enabled") ?? true;

    // ---------------------------------------------------------------------------
    // Check if unconfigured or missing local dependencies
    // ---------------------------------------------------------------------------
    var ollamaModel = ollamaSection["Model"];
    bool ollamaMissing = false;
    if (!string.IsNullOrEmpty(ollamaModel) && ollamaEnabled)
    {
        // Use detector to see if ollama is actually installed or running
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
        llamaSharpEnabled: llamaSharpEnabled);

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
    // AI Providers
    // ------------------------------------------------------------------
    builder.Services.Configure<AiProviderOptions>("openai", openAiSection);
    builder.Services.Configure<AiProviderOptions>("anthropic", anthropicSection);
    builder.Services.Configure<AiProviderOptions>("augment", augmentSection);
    builder.Services.Configure<AiProviderOptions>("augmentchat", augmentChatSection);
    builder.Services.Configure<AiProviderOptions>("ollama", ollamaSection);
    builder.Services.Configure<AiProviderOptions>("llamasharp", llamaSharpSection);

    if (openAiEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new OpenAiProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("openai")),
                sp.GetRequiredService<ILogger<OpenAiProvider>>()));
    }

    if (anthropicEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new AnthropicProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("anthropic")),
                sp.GetRequiredService<ILogger<AnthropicProvider>>()));
    }

    if (augmentEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new AugmentMcpProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("augment")),
                sp.GetRequiredService<ILogger<AugmentMcpProvider>>()));
    }

    if (augmentChatEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new AugmentChatProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("augmentchat")),
                sp.GetRequiredService<ILogger<AugmentChatProvider>>()));
    }

    if (ollamaEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new OllamaProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("ollama")),
                sp.GetRequiredService<ILogger<OllamaProvider>>()));
    }

    if (llamaSharpEnabled)
    {
        builder.Services.AddSingleton<IAiProvider>(sp =>
            new LLamaSharpProvider(
                Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("llamasharp")),
                sp.GetRequiredService<ILogger<LLamaSharpProvider>>()));
    }

    builder.Services.AddSingleton<OllamaDetector>();

    // ------------------------------------------------------------------
    // Communication Adapters
    // ------------------------------------------------------------------
    builder.Services.Configure<SlackAdapterOptions>(slackSection);
    builder.Services.Configure<TeamsAdapterOptions>(teamsSection);
    builder.Services.Configure<SignalAdapterOptions>(signalSection);

    if (signalEnabled)
    {
        builder.Services.AddHttpClient(nameof(SignalAdapter));
    }

    if (cliEnabled)
    {
        builder.Services.AddSingleton<ICommunicationAdapter, CliAdapter>();
    }

    if (slackEnabled)
    {
        builder.Services.AddSingleton<ICommunicationAdapter, SlackAdapter>();
    }

    if (teamsEnabled)
    {
        builder.Services.AddSingleton<ICommunicationAdapter, TeamsAdapter>();

        // Bot Framework adapter for Teams webhook integration.
        // ConfigurationBotFrameworkAuthentication reads MicrosoftAppId/Password from the
        // provided IConfiguration section — we pass the Communication:Teams subsection so
        // it picks up our credentials rather than looking at the root config.
        builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp =>
            new CloudAdapter(
                new ConfigurationBotFrameworkAuthentication(teamsSection),
                sp.GetRequiredService<ILogger<CloudAdapter>>()));
    }

    if (signalEnabled)
    {
        builder.Services.AddSingleton<ICommunicationAdapter, SignalAdapter>();
    }

    // ------------------------------------------------------------------
    // Integration Tools
    // ------------------------------------------------------------------
    builder.Services.Configure<GitHubToolOptions>(config.GetSection("Integrations:GitHub"));
    builder.Services.Configure<SentryToolOptions>(config.GetSection("Integrations:Sentry"));
    builder.Services.Configure<Office365CalendarToolOptions>(config.GetSection("Integrations:Office365Calendar"));
    builder.Services.Configure<GmailToolOptions>(config.GetSection("Integrations:Gmail"));

    builder.Services.AddSingleton<IIntegrationTool, GitHubTool>();
    builder.Services.AddSingleton<IIntegrationTool, SentryTool>();
    builder.Services.AddSingleton<IIntegrationTool, Office365CalendarTool>();
    builder.Services.AddSingleton<GmailTool>();
    builder.Services.AddSingleton<IIntegrationTool>(sp => sp.GetRequiredService<GmailTool>());

    // ------------------------------------------------------------------
    // Conversation Session Store
    // ------------------------------------------------------------------
    builder.Services.Configure<ConversationSessionOptions>(config.GetSection("ConversationSessions"));
    builder.Services.AddSingleton<IConversationSessionStore, JsonlConversationSessionStore>();

    // ------------------------------------------------------------------
    // Skill System
    // ------------------------------------------------------------------
    builder.Services.AddSingleton<PromptSkillLoader>();
    builder.Services.AddSingleton<PromptSkillRunner>();
    builder.Services.AddSingleton<CompiledSkillRunner>();
    builder.Services.AddSingleton<ISkillRouter, SkillRouter>();
    builder.Services.AddSingleton<ISkill, StandupHelperSkill>();
    builder.Services.AddSingleton<ISkill, GmailSkill>();

    // ------------------------------------------------------------------
    // Standup Scheduler
    // ------------------------------------------------------------------
    builder.Services.Configure<StandupSchedulerOptions>(config.GetSection("StandupScheduler"));

    // ------------------------------------------------------------------
    // Gmail Scheduler
    // ------------------------------------------------------------------
    builder.Services.Configure<GmailSchedulerOptions>(config.GetSection("GmailScheduler"));

    // ------------------------------------------------------------------
    // Sentry Bugfix Automation
    // ------------------------------------------------------------------
    builder.Services.Configure<SentryBugfixOptions>(config.GetSection("SentryBugfix"));
    builder.Services.AddSingleton<IGitCommandRunner, GitCommandRunner>();
    builder.Services.AddSingleton<IAuggieCliRunner, AuggieCliRunner>();
    builder.Services.AddSingleton<IPullRequestCreator, OctokitPullRequestCreator>();
    builder.Services.AddSingleton<SentryBugfixService>();

    // ------------------------------------------------------------------
    // Background Services
    // ------------------------------------------------------------------
    builder.Services.AddHostedService<UpdateService>();
    builder.Services.AddHostedService<MessageProcessingService>();
    builder.Services.AddHostedService<StandupSchedulerService>();
    builder.Services.AddHostedService<GmailSchedulerService>();
    builder.Services.AddHostedService<GmailWatchRenewalService>();

    // ------------------------------------------------------------------
    // Build & Configure Pipeline
    // ------------------------------------------------------------------
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "0.1.0" }));

    // Readiness info
    app.MapGet("/", () => Results.Ok(new
    {
        name = "Anduril AI Assistant",
        version = "0.1.0",
        status = "running"
    }));

    // ------------------------------------------------------------------
    // Gmail Push Notification Endpoint (Pub/Sub → Anduril)
    // ------------------------------------------------------------------
    app.MapPost("/api/gmail/push", async (
        HttpContext context,
        GmailTool gmailTool,
        IEnumerable<ICommunicationAdapter> adapters,
        ILogger<Program> endpointLogger) =>
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            // Pub/Sub sends: { "message": { "data": "<base64>", "messageId": "..." }, "subscription": "..." }
            using var doc = JsonDocument.Parse(body);
            var dataBase64 = doc.RootElement
                .GetProperty("message")
                .GetProperty("data")
                .GetString();

            if (string.IsNullOrEmpty(dataBase64))
            {
                endpointLogger.LogWarning("Gmail push notification received with empty data.");
                return Results.Ok(); // ACK to avoid redelivery
            }

            var dataJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(dataBase64));
            using var dataDoc = JsonDocument.Parse(dataJson);

            // Gmail notification payload: { "emailAddress": "...", "historyId": 12345 }
            var historyId = dataDoc.RootElement.GetProperty("historyId").GetUInt64();

            endpointLogger.LogInformation(
                "Gmail push notification received. History ID: {HistoryId}", historyId);

            if (!gmailTool.IsAvailable)
            {
                endpointLogger.LogWarning("Gmail tool not available. Ignoring push notification.");
                return Results.Ok();
            }

            var notifications = await gmailTool.ProcessPushNotificationAsync(historyId);

            // Send any rule-triggered notifications to connected adapters
            foreach (var notification in notifications)
            {
                var adapter = adapters.FirstOrDefault(a => a.IsConnected);
                if (adapter is null) break;

                try
                {
                    await adapter.SendMessageAsync(new OutgoingMessage
                    {
                        Text = notification,
                        ChannelId = app.Configuration["GmailScheduler:TargetChannel"] ?? ""
                    });
                }
                catch (Exception ex)
                {
                    endpointLogger.LogError(ex,
                        "Failed to send Gmail notification through {Platform}", adapter.Platform);
                }
            }

            return Results.Ok();
        }
        catch (Exception ex)
        {
            endpointLogger.LogError(ex, "Error processing Gmail push notification");
            return Results.Ok(); // ACK to Pub/Sub even on error to prevent infinite redelivery
        }
    });

    // ------------------------------------------------------------------
    // Sentry Webhook Endpoint (Sentry → Anduril)
    // ------------------------------------------------------------------
    var webhookSemaphore = new SemaphoreSlim(3);
    app.MapPost("/webhooks/sentry", async (
        HttpContext context,
        SentryBugfixService bugfixService,
        IOptions<SentryBugfixOptions> bugfixOptions,
        IHostApplicationLifetime lifetime,
        ILogger<Program> endpointLogger) =>
    {
        try
        {
            // Read the raw body bytes for HMAC validation and deserialization
            using var ms = new MemoryStream();
            await context.Request.Body.CopyToAsync(ms);
            var bodyBytes = ms.ToArray();

            // Require webhook secret when the feature is enabled to prevent unauthenticated requests
            var secret = bugfixOptions.Value.WebhookSecret;
            if (string.IsNullOrEmpty(secret))
            {
                if (bugfixOptions.Value.Enabled)
                {
                    endpointLogger.LogError("Sentry bugfix is enabled but WebhookSecret is not configured. Rejecting request.");
                    return Results.StatusCode(403);
                }
            }
            else
            {
                var signature = context.Request.Headers["sentry-hook-signature"].FirstOrDefault();
                if (string.IsNullOrEmpty(signature))
                {
                    endpointLogger.LogWarning("Sentry webhook missing sentry-hook-signature header.");
                    return Results.Unauthorized();
                }

                var keyBytes = Encoding.UTF8.GetBytes(secret);
                var computedHash = HMACSHA256.HashData(keyBytes, bodyBytes);

                byte[] signatureBytes;
                try
                {
                    signatureBytes = Convert.FromHexString(signature);
                }
                catch (FormatException)
                {
                    endpointLogger.LogWarning("Sentry webhook signature is not valid hex.");
                    return Results.Unauthorized();
                }

                if (!CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes))
                {
                    endpointLogger.LogWarning("Sentry webhook signature mismatch.");
                    return Results.Unauthorized();
                }
            }

            var payload = JsonSerializer.Deserialize<SentryWebhookPayload>(bodyBytes);
            if (payload is null)
            {
                endpointLogger.LogWarning("Received null Sentry webhook payload.");
                return Results.BadRequest("Invalid payload.");
            }

            endpointLogger.LogInformation(
                "Sentry webhook received: action={Action}, issue={IssueId}",
                payload.Action, payload.Data.Issue.Id);

            // Limit concurrent pipeline executions to prevent resource exhaustion
            if (!webhookSemaphore.Wait(0))
            {
                endpointLogger.LogWarning("Sentry webhook rejected: max concurrent bugfix pipelines reached.");
                return Results.StatusCode(429);
            }

            // Fire-and-forget with failure logging and semaphore release
            _ = Task.Run(async () =>
            {
                try
                {
                    await bugfixService.HandleWebhookAsync(payload, lifetime.ApplicationStopping);
                }
                catch (Exception ex)
                {
                    endpointLogger.LogError(ex,
                        "Unhandled exception in Sentry bugfix pipeline for issue {IssueId}",
                        payload.Data.Issue.Id);
                }
                finally
                {
                    webhookSemaphore.Release();
                }
            }, CancellationToken.None);

            return Results.Ok(new { status = "accepted" });
        }
        catch (Exception ex)
        {
            endpointLogger.LogError(ex, "Error processing Sentry webhook");
            return Results.StatusCode(500);
        }
    });

    if (teamsEnabled)
    {
        // ------------------------------------------------------------------
        // Teams Bot Framework Webhook Endpoint (Teams → Anduril)
        // ------------------------------------------------------------------
        app.MapPost("/api/teams/messages", async (
            HttpContext httpContext,
            IBotFrameworkHttpAdapter adapter,
            IEnumerable<ICommunicationAdapter> adapters) =>
        {
            var teamsAdapter = adapters.OfType<TeamsAdapter>().FirstOrDefault();
            if (teamsAdapter is null)
            {
                return Results.StatusCode(500);
            }

            await adapter.ProcessAsync(httpContext.Request, httpContext.Response, new TeamsBot(teamsAdapter));
            return Results.Empty;
        });
    }

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
