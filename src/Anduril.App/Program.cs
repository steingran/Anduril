using Anduril.App.Services;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI.Avalonia;
using Serilog;
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
    AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
    {
        Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled AppDomain exception terminated the desktop app");
    };

    TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
    {
        Log.Error(eventArgs.Exception, "Unobserved task exception in desktop app");
        eventArgs.SetObserved();
    };

    Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
    {
        Log.Fatal(eventArgs.Exception, "Unhandled UI-thread exception terminated the desktop app");
    };

    Log.Information("Launching Avalonia desktop lifetime");

    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    Log.Information("Avalonia desktop lifetime exited");

    // Dispose the SignalR chat service after the UI lifetime has exited so the
    // async dispose never runs on (or blocks) the UI thread.
    if (Avalonia.Application.Current is Anduril.App.App app && app.ChatService is { } chat)
    {
        await chat.DisposeAsync();
    }
}
finally
{
    Log.Information("Stopping embedded host service");
    await hostService.StopAsync();
}

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<Anduril.App.App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI(_ => { })
        .LogToTrace();
