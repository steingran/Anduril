using System.Text.Json;
using Anduril.AI.Detection;
using Anduril.AI.Providers;
using Anduril.Communication;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Host;
using Anduril.Host.Services;
using Anduril.Integrations;
using Anduril.Skills;
using Anduril.Skills.Compiled;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Options;
using Serilog;

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
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    var config = builder.Configuration;

    // ------------------------------------------------------------------
    // AI Providers
    // ------------------------------------------------------------------
    builder.Services.Configure<AiProviderOptions>("openai", config.GetSection("AI:OpenAI"));
    builder.Services.Configure<AiProviderOptions>("anthropic", config.GetSection("AI:Anthropic"));
    builder.Services.Configure<AiProviderOptions>("augment", config.GetSection("AI:Augment"));
    builder.Services.Configure<AiProviderOptions>("augmentchat", config.GetSection("AI:AugmentChat"));
    builder.Services.Configure<AiProviderOptions>("ollama", config.GetSection("AI:Ollama"));
    builder.Services.Configure<AiProviderOptions>("llamasharp", config.GetSection("AI:LLamaSharp"));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new OpenAiProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("openai")),
            sp.GetRequiredService<ILogger<OpenAiProvider>>()));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new AnthropicProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("anthropic")),
            sp.GetRequiredService<ILogger<AnthropicProvider>>()));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new AugmentMcpProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("augment")),
            sp.GetRequiredService<ILogger<AugmentMcpProvider>>()));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new AugmentChatProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("augmentchat")),
            sp.GetRequiredService<ILogger<AugmentChatProvider>>()));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new OllamaProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("ollama")),
            sp.GetRequiredService<ILogger<OllamaProvider>>()));

    builder.Services.AddSingleton<IAiProvider>(sp =>
        new LLamaSharpProvider(
            Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("llamasharp")),
            sp.GetRequiredService<ILogger<LLamaSharpProvider>>()));

    builder.Services.AddSingleton<OllamaDetector>();

    // ------------------------------------------------------------------
    // Communication Adapters
    // ------------------------------------------------------------------
    builder.Services.Configure<SlackAdapterOptions>(config.GetSection("Communication:Slack"));
    builder.Services.Configure<TeamsAdapterOptions>(config.GetSection("Communication:Teams"));
    builder.Services.Configure<SignalAdapterOptions>(config.GetSection("Communication:Signal"));
    builder.Services.AddHttpClient(nameof(SignalAdapter));

    builder.Services.AddSingleton<ICommunicationAdapter, CliAdapter>();
    builder.Services.AddSingleton<ICommunicationAdapter, SlackAdapter>();
    builder.Services.AddSingleton<ICommunicationAdapter, TeamsAdapter>();
    builder.Services.AddSingleton<ICommunicationAdapter, SignalAdapter>();

    // Bot Framework adapter for Teams webhook integration.
    // ConfigurationBotFrameworkAuthentication reads MicrosoftAppId/Password from the
    // provided IConfiguration section — we pass the Communication:Teams subsection so
    // it picks up our credentials rather than looking at the root config.
    builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp =>
        new CloudAdapter(
            new ConfigurationBotFrameworkAuthentication(config.GetSection("Communication:Teams")),
            sp.GetRequiredService<ILogger<CloudAdapter>>()));

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
