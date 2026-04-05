using System.Text.Json;
using Anduril.AI.Detection;
using Anduril.AI.Providers;
using Anduril.Communication;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.MenuPlanning;
using Anduril.Core.Skills;
using Anduril.Core.Webhooks;
using Anduril.Host.Hubs;
using Anduril.Host.Services;
using Anduril.Integrations;
using Anduril.Skills;
using Anduril.Skills.Compiled;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Options;

namespace Anduril.Host;

/// <summary>
/// Marker class for endpoint logger category since Program is not accessible from extension methods.
/// </summary>
internal sealed class EndpointLogger;

/// <summary>
/// Extracts all Anduril DI registration and endpoint mapping from Program.cs
/// so it can be shared between the standalone host and the desktop application.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Registers all Anduril services: AI providers, communication adapters,
    /// integration tools, skills, SignalR, background services, and options.
    /// </summary>
    public static WebApplicationBuilder AddAndurilServices(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var openAiSection = config.GetSection("AI:OpenAI");
        var anthropicSection = config.GetSection("AI:Anthropic");
        var augmentSection = config.GetSection("AI:Augment");
        var augmentChatSection = config.GetSection("AI:AugmentChat");
        var ollamaSection = config.GetSection("AI:Ollama");
        var llamaSharpSection = config.GetSection("AI:LLamaSharp");
        var copilotSection = config.GetSection("AI:Copilot");
        var cliSection = config.GetSection("Communication:Cli");
        var slackSection = config.GetSection("Communication:Slack");
        var teamsSection = config.GetSection("Communication:Teams");
        var signalSection = config.GetSection("Communication:Signal");
        var protonMailSection = config.GetSection("Integrations:ProtonMail");

        bool openAiEnabled = openAiSection.GetValue<bool?>("Enabled") ?? true;
        bool anthropicEnabled = anthropicSection.GetValue<bool?>("Enabled") ?? true;
        bool augmentEnabled = augmentSection.GetValue<bool?>("Enabled") ?? true;
        bool augmentChatEnabled = augmentChatSection.GetValue<bool?>("Enabled") ?? true;
        bool ollamaEnabled = ollamaSection.GetValue<bool?>("Enabled") ?? true;
        bool llamaSharpEnabled = llamaSharpSection.GetValue<bool?>("Enabled") ?? true;
        bool copilotEnabled = copilotSection.GetValue<bool?>("Enabled") ?? false;
        bool cliEnabled = cliSection.GetValue<bool?>("Enabled") ?? true;
        bool slackEnabled = slackSection.GetValue<bool?>("Enabled") ?? true;
        bool teamsEnabled = teamsSection.GetValue<bool?>("Enabled") ?? true;
        bool signalEnabled = signalSection.GetValue<bool?>("Enabled") ?? true;
        bool protonMailEnabled = protonMailSection.GetValue<bool?>("Enabled") ?? true;
        bool weeklyMenuPlannerEnabled = config.GetSection("WeeklyMenuPlanner").GetValue<bool?>("Enabled") ?? true;

        // ------------------------------------------------------------------
        // AI Providers
        // ------------------------------------------------------------------
        builder.Services.Configure<AiProviderOptions>("openai", openAiSection);
        builder.Services.Configure<AiProviderOptions>("anthropic", anthropicSection);
        builder.Services.Configure<AiProviderOptions>("augment", augmentSection);
        builder.Services.Configure<AiProviderOptions>("augmentchat", augmentChatSection);
        builder.Services.Configure<AiProviderOptions>("ollama", ollamaSection);
        builder.Services.Configure<AiProviderOptions>("llamasharp", llamaSharpSection);
        builder.Services.Configure<AiProviderOptions>("copilot", copilotSection);

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

        if (copilotEnabled)
        {
            builder.Services.AddSingleton<IAiProvider>(sp =>
                new CopilotProvider(
                    Options.Create(sp.GetRequiredService<IOptionsMonitor<AiProviderOptions>>().Get("copilot")),
                    sp.GetRequiredService<ILogger<CopilotProvider>>()));
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
        builder.Services.Configure<ProtonMailToolOptions>(protonMailSection);
        builder.Services.Configure<SlackQueryToolOptions>(config.GetSection("Integrations:SlackQuery"));
        builder.Services.Configure<MediumArticleToolOptions>(config.GetSection("Integrations:MediumArticle"));
        builder.Services.Configure<WeeklyMenuPlannerOptions>(config.GetSection("WeeklyMenuPlanner"));
        builder.Services.AddHttpClient(SentryTool.HttpClientName);
        builder.Services.AddHttpClient(nameof(MediumArticleTool));

        builder.Services.AddSingleton<IIntegrationTool, GitHubTool>();
        builder.Services.AddSingleton<IIntegrationTool, SentryTool>();
        builder.Services.AddSingleton<IIntegrationTool, Office365CalendarTool>();
        builder.Services.AddSingleton<GmailTool>();
        builder.Services.AddSingleton<IIntegrationTool>(sp => sp.GetRequiredService<GmailTool>());

        if (protonMailEnabled)
        {
            builder.Services.AddSingleton<IIntegrationTool, ProtonMailTool>();
        }

        builder.Services.AddSingleton<IIntegrationTool, SlackQueryTool>();
        builder.Services.AddSingleton<IIntegrationTool, MediumArticleTool>();

        if (weeklyMenuPlannerEnabled)
        {
            builder.Services.AddSingleton<IWeeklyMenuSubscriptionStore, SqliteWeeklyMenuSubscriptionStore>();
            builder.Services.AddSingleton<IIntegrationTool, WeeklyMenuPlannerTool>();
        }

        // ------------------------------------------------------------------
        // Conversation Session Store
        // ------------------------------------------------------------------
        builder.Services.Configure<ConversationSessionOptions>(config.GetSection("ConversationSessions"));
        builder.Services.AddSingleton<IConversationSessionStore, JsonlConversationSessionStore>();

        // ------------------------------------------------------------------
        // Skill System
        // ------------------------------------------------------------------
        builder.Services.Configure<SkillsOptions>(config.GetSection("Skills"));
        builder.Services.AddSingleton<PromptSkillLoader>();
        builder.Services.AddSingleton<PromptSkillRunner>(sp =>
        {
            var skillOptions = sp.GetRequiredService<IOptions<SkillsOptions>>().Value;

            return new PromptSkillRunner(
                sp.GetRequiredService<PromptSkillLoader>(),
                sp.GetServices<IAiProvider>(),
                sp.GetServices<IIntegrationTool>(),
                sp.GetRequiredService<ILogger<PromptSkillRunner>>(),
                skillOptions.SkillsDirectory,
                skillOptions.LocalSkillsDirectory);
        });
        builder.Services.AddSingleton<CompiledSkillRunner>();
        builder.Services.AddSingleton<ISkillRouter, SkillRouter>();
        builder.Services.AddSingleton<ISkill, StandupHelperSkill>();
        builder.Services.AddSingleton<ISkill, GmailSkill>();

        // ------------------------------------------------------------------
        // Schedulers & Automation
        // ------------------------------------------------------------------
        builder.Services.Configure<StandupSchedulerOptions>(config.GetSection("StandupScheduler"));
        builder.Services.Configure<GmailSchedulerOptions>(config.GetSection("GmailScheduler"));
        builder.Services.Configure<SentryBugfixOptions>(config.GetSection("SentryBugfix"));
        builder.Services.AddSingleton<IGitCommandRunner, GitCommandRunner>();
        builder.Services.AddSingleton<IAuggieCliRunner, AuggieCliRunner>();
        builder.Services.AddSingleton<IShellCommandRunner, ShellCommandRunner>();
        builder.Services.AddSingleton<IPullRequestCreator, OctokitPullRequestCreator>();
        builder.Services.AddSingleton<SentryWebhookRequestValidator>();
        builder.Services.AddSingleton<SentryBugfixService>();

        // ------------------------------------------------------------------
        // SignalR (for desktop app communication)
        // ------------------------------------------------------------------
        builder.Services.AddSignalR()
            .AddHubOptions<Anduril.Host.Hubs.AndurilChatHub>(options =>
            {
                // Allow CancelMessage to be dispatched while SendMessage is still streaming.
                // Default is 1 (sequential), which would queue CancelMessage behind the
                // in-flight SendMessage and make the Stop button ineffective.
                options.MaximumParallelInvocationsPerClient = 2;
            });

        // ------------------------------------------------------------------
        // Background Services
        // ------------------------------------------------------------------
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddHostedService<MessageProcessingService>();

        if (weeklyMenuPlannerEnabled)
        {
            builder.Services.AddHostedService<WeeklyMenuPlannerSchedulerService>();
        }

        builder.Services.AddHostedService<StandupSchedulerService>();
        builder.Services.AddHostedService<GmailSchedulerService>();
        builder.Services.AddHostedService<GmailWatchRenewalService>();

        return builder;
    }

    /// <summary>
    /// Maps all Anduril HTTP endpoints: health check, SignalR hub, Gmail push,
    /// Sentry webhook, and Teams webhook.
    /// </summary>
    public static WebApplication MapAndurilEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "0.1.0" }));

        // Tool inspector endpoint — lists available AI providers and integration tools
        app.MapGet("/api/tools", async (
            IEnumerable<IIntegrationTool> integrationTools,
            IEnumerable<IAiProvider> aiProviders) =>
        {
            var integrationList = integrationTools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                available = t.IsAvailable,
                functions = t.IsAvailable
                    ? t.GetFunctions().Select(f => f.Name).ToList()
                    : (List<string>)[]
            }).ToList<object>();

            // Repo-scoped tools are created dynamically per-request in the hub (not registered as
            // IIntegrationTool), so they must be advertised here as a static entry.
            integrationList.Add(new
            {
                name = "code",
                description = "Tools for exploring a git repository selected in the Code tab. Available for all non-Copilot providers.",
                available = true,
                functions = new List<string> { "repo_read_file", "repo_list_files", "repo_run_git" }
            });

            var providers = await Task.WhenAll(aiProviders.Select(async p =>
            {
                var tools = p.IsAvailable
                    ? (await p.GetToolsAsync()).Select(t => t.Name).ToList()
                    : (List<string>)[];
                return new
                {
                    name = p.Name,
                    available = p.IsAvailable,
                    supportsChatCompletion = p.SupportsChatCompletion,
                    tools
                };
            }));

            return Results.Ok(new { integrations = integrationList, providers });
        });

        // Readiness info
        app.MapGet("/", () => Results.Ok(new
        {
            name = "Anduril AI Assistant",
            version = "0.1.0",
            status = "running"
        }));

        // SignalR Hub (Desktop App → Anduril)
        app.MapHub<AndurilChatHub>("/hubs/chat");

        // Gmail Push Notification Endpoint (Pub/Sub → Anduril)
        app.MapPost("/api/gmail/push", async (
            HttpContext context,
            GmailTool gmailTool,
            IEnumerable<ICommunicationAdapter> adapters,
            ILogger<EndpointLogger> endpointLogger) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(body);
                var dataBase64 = doc.RootElement
                    .GetProperty("message")
                    .GetProperty("data")
                    .GetString();

                if (string.IsNullOrEmpty(dataBase64))
                {
                    endpointLogger.LogWarning("Gmail push notification received with empty data.");
                    return Results.Ok();
                }

                var dataJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(dataBase64));
                using var dataDoc = JsonDocument.Parse(dataJson);

                var historyIdElement = dataDoc.RootElement.GetProperty("historyId");
                var historyId = historyIdElement.ValueKind == System.Text.Json.JsonValueKind.String
                    ? ulong.Parse(historyIdElement.GetString()!)
                    : historyIdElement.GetUInt64();

                endpointLogger.LogInformation(
                    "Gmail push notification received. History ID: {HistoryId}", historyId);

                if (!gmailTool.IsAvailable)
                {
                    endpointLogger.LogWarning("Gmail tool not available. Ignoring push notification.");
                    return Results.Ok();
                }

                var notifications = await gmailTool.ProcessPushNotificationAsync(historyId);

                foreach (var notification in notifications)
                {
                    bool sent = false;
                    foreach (var adapter in adapters.Where(a => a.IsConnected))
                    {
                        try
                        {
                            await adapter.SendMessageAsync(new OutgoingMessage
                            {
                                Text = notification,
                                ChannelId = app.Configuration["GmailScheduler:TargetChannel"] ?? ""
                            });
                            sent = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            endpointLogger.LogError(ex,
                                "Failed to send Gmail notification through {Platform}, trying next adapter", adapter.Platform);
                        }
                    }

                    if (!sent)
                        endpointLogger.LogWarning("No connected adapter could deliver Gmail notification");
                }

                return Results.Ok();
            }
            catch (Exception ex)
            {
                endpointLogger.LogError(ex, "Error processing Gmail push notification");
                return Results.Ok();
            }
        });

        // Sentry Webhook Endpoint (Sentry → Anduril)
        var webhookSemaphore = new SemaphoreSlim(3);
        // Note: intentionally NOT disposing webhookSemaphore on ApplicationStopping.
        // Disposing it races with in-flight background tasks whose finally blocks call
        // Release(), throwing ObjectDisposedException during shutdown. The GC handles
        // the unmanaged wait handle (if any) when the process exits.
        app.MapPost("/webhooks/sentry", async (
            HttpContext context,
            SentryBugfixService bugfixService,
            SentryWebhookRequestValidator webhookValidator,
            IOptions<SentryBugfixOptions> bugfixOptions,
            IHostApplicationLifetime lifetime,
            ILogger<EndpointLogger> endpointLogger) =>
        {
            try
            {
                using var ms = new MemoryStream();
                await context.Request.Body.CopyToAsync(ms);
                var bodyBytes = ms.ToArray();
                var validationResult = webhookValidator.Validate(
                    bodyBytes,
                    context.Request.Headers["sentry-hook-signature"].FirstOrDefault(),
                    bugfixOptions.Value);

                if (!validationResult.IsValid)
                {
                    if (validationResult.StatusCode == 400)
                        return Results.BadRequest(validationResult.ErrorMessage ?? "Invalid payload.");

                    return Results.StatusCode(validationResult.StatusCode ?? 500);
                }

                var payload = validationResult.Payload!;

                endpointLogger.LogInformation(
                    "Sentry webhook received: action={Action}, issue={IssueId}",
                    payload.Action, payload.Data.Issue.Id);

                if (!webhookSemaphore.Wait(0))
                {
                    endpointLogger.LogWarning("Sentry webhook rejected: max concurrent bugfix pipelines reached.");
                    return Results.StatusCode(429);
                }

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

        // Teams Bot Framework Webhook Endpoint (Teams → Anduril)
        bool teamsEnabled = app.Configuration.GetSection("Communication:Teams").GetValue<bool?>("Enabled") ?? true;
        if (teamsEnabled)
        {
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

        return app;
    }
}
