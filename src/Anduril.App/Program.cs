using Anduril.App.Services;
using Avalonia;
using Avalonia.ReactiveUI;
using Velopack;

// ---------------------------------------------------------------------------
// Velopack update hooks — must run before anything else
// ---------------------------------------------------------------------------
VelopackApp.Build().Run();

// ---------------------------------------------------------------------------
// Start the embedded ASP.NET Core host, then launch the Avalonia UI
// ---------------------------------------------------------------------------
var hostService = new HostService(args);
await hostService.StartAsync();

try
{
    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
finally
{
    await hostService.StopAsync();
}

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<Anduril.App.App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI()
        .LogToTrace();
