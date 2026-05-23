using Anduril.App.Services;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;

namespace Anduril.App;

public class App : Application
{
    /// <summary>
    /// The chat service created when the desktop lifetime is initialized.
    /// Exposed so the bootstrap (Program.cs) can dispose it asynchronously
    /// after the UI lifetime has exited, avoiding a blocking dispose on the
    /// UI thread during shutdown.
    /// </summary>
    public SignalRChatService? ChatService { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Information("Initializing classic desktop lifetime against host {BaseUrl}", HostService.BaseUrl);

            ChatService = new SignalRChatService(HostService.BaseUrl);
            var prefsService = new UserPreferencesService();
            var mainVm = new MainWindowViewModel(ChatService, prefsService);
            var mainWindow = new MainWindow { DataContext = mainVm };

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Startup += (_, _) =>
            {
                Log.Information("Desktop startup event received; showing main window");
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
                Log.Information(
                    "Main window show requested. Visible={IsVisible}, WindowState={WindowState}, ShowInTaskbar={ShowInTaskbar}",
                    mainWindow.IsVisible,
                    mainWindow.WindowState,
                    mainWindow.ShowInTaskbar);
            };
            desktop.Exit += (_, _) =>
            {
                Log.Information(
                    "Desktop exit event received. MainWindowVisible={IsVisible}",
                    desktop.MainWindow?.IsVisible);
            };

            mainWindow.Opened += (_, _) =>
            {
                Log.Information(
                    "Main window opened. Visible={IsVisible}, WindowState={WindowState}, Bounds={Bounds}",
                    mainWindow.IsVisible,
                    mainWindow.WindowState,
                    mainWindow.Bounds);
            };
            mainWindow.Closing += (_, e) =>
            {
                Log.Warning(
                    "Main window closing requested. Visible={IsVisible}, WindowState={WindowState}, Cancel={Cancel}",
                    mainWindow.IsVisible,
                    mainWindow.WindowState,
                    e.Cancel);
            };
            mainWindow.Closed += (_, _) =>
            {
                Log.Warning("Main window closed");
            };

            desktop.MainWindow = mainWindow;
            Log.Information("Assigned main window to desktop lifetime");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
