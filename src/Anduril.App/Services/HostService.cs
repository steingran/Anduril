using Anduril.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Anduril.App.Services;

/// <summary>
/// Manages the embedded ASP.NET Core host lifecycle.
/// Starts on a background thread with an OS-assigned port.
/// </summary>
public sealed class HostService
{
    private WebApplication? _app;
    private Task? _runTask;

    /// <summary>
    /// The base URL of the running host (e.g. "http://localhost:54321").
    /// Set after <see cref="StartAsync"/> completes.
    /// </summary>
    public static string BaseUrl { get; private set; } = string.Empty;

    private readonly string[] _args;

    public HostService(string[] args)
    {
        _args = args;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder(_args);

        // Bind to a random available port
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Disable CLI adapter for desktop mode
        builder.Configuration["Communication:Cli:Enabled"] = "false";

        // Serilog integration
        builder.Host.UseSerilog((ctx, lc) => lc
            .WriteTo.Console()
            .ReadFrom.Configuration(ctx.Configuration));

        builder.AddAndurilServices();

        _app = builder.Build();

        _app.UseSerilogRequestLogging();
        _app.MapAndurilEndpoints();

        // Start the host without blocking
        await _app.StartAsync();

        // Resolve the actual port the OS assigned
        var server = _app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        var address = addressFeature?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:5000";
        BaseUrl = address;

        // Run in background
        _runTask = _app.WaitForShutdownAsync();
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_runTask is not null)
        {
            await _runTask;
        }
    }
}
