using Anduril.AI.Detection;
using Anduril.AI.Providers;
using Anduril.Communication;
using Anduril.Core.AI;
using Anduril.Core.Communication;
using Anduril.Core.Integrations;
using Anduril.Core.Skills;
using Anduril.Host;
using Anduril.Integrations;
using Anduril.Skills;
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

    builder.Services.AddSingleton<ICommunicationAdapter, CliAdapter>();
    builder.Services.AddSingleton<ICommunicationAdapter, SlackAdapter>();
    builder.Services.AddSingleton<ICommunicationAdapter, TeamsAdapter>();

    // ------------------------------------------------------------------
    // Integration Tools
    // ------------------------------------------------------------------
    builder.Services.Configure<GitHubToolOptions>(config.GetSection("Integrations:GitHub"));
    builder.Services.Configure<SentryToolOptions>(config.GetSection("Integrations:Sentry"));
    builder.Services.Configure<CalendarToolOptions>(config.GetSection("Integrations:Calendar"));

    builder.Services.AddSingleton<IIntegrationTool, GitHubTool>();
    builder.Services.AddSingleton<IIntegrationTool, SentryTool>();
    builder.Services.AddSingleton<IIntegrationTool, CalendarTool>();

    // ------------------------------------------------------------------
    // Skill System
    // ------------------------------------------------------------------
    builder.Services.AddSingleton<PromptSkillLoader>();
    builder.Services.AddSingleton<PromptSkillRunner>();
    builder.Services.AddSingleton<CompiledSkillRunner>();
    builder.Services.AddSingleton<ISkillRouter, SkillRouter>();

    // ------------------------------------------------------------------
    // Background Services
    // ------------------------------------------------------------------
    builder.Services.AddHostedService<UpdateService>();
    builder.Services.AddHostedService<MessageProcessingService>();

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
