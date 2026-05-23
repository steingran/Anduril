using Anduril.App.Services;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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

            Window mainWindow;
            try
            {
                mainWindow = new MainWindow();
                Log.Information("Constructed main window shell");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "MainWindow construction failed; falling back to diagnostic window");
                mainWindow = CreateDiagnosticWindow(ex);
            }

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Startup += (_, _) =>
            {
                Log.Information("Desktop startup event received");
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

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (!mainWindow.IsVisible)
                    {
                        Log.Information("Dispatcher startup show requested for main window");
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                        mainWindow.Focus();
                        Log.Information(
                            "Main window dispatcher show completed. Visible={IsVisible}, WindowState={WindowState}, ShowInTaskbar={ShowInTaskbar}",
                            mainWindow.IsVisible,
                            mainWindow.WindowState,
                            mainWindow.ShowInTaskbar);
                    }
                    else
                    {
                        Log.Information(
                            "Main window already visible after lifetime assignment. WindowState={WindowState}, ShowInTaskbar={ShowInTaskbar}",
                            mainWindow.WindowState,
                            mainWindow.ShowInTaskbar);
                    }

                    Log.Information("Creating desktop services and main window view model");
                    ChatService = new SignalRChatService(HostService.BaseUrl);
                    var prefsService = new UserPreferencesService();
                    var mainVm = new MainWindowViewModel(ChatService, prefsService);
                    mainWindow.DataContext = mainVm;
                    Log.Information("Assigned main window data context");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed while initializing the main window shell or data context");
                    throw;
                }
            }, DispatcherPriority.Loaded);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateDiagnosticWindow(Exception ex)
    {
        return new Window
        {
            Title = "Anduril Startup Error",
            Width = 960,
            Height = 640,
            MinWidth = 720,
            MinHeight = 480,
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Anduril failed to construct the main window.",
                                FontSize = 22,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = Brushes.Black
                            },
                            new TextBlock
                            {
                                Text = "This fallback window is shown so startup exceptions are visible on the desktop. Please copy the exception below back into the issue thread.",
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = Brushes.Black
                            },
                            new Border
                            {
                                Background = Brushes.WhiteSmoke,
                                Padding = new Thickness(12),
                                MinHeight = 420,
                                Child = new TextBlock
                                {
                                    Text = ex.ToString(),
                                    FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                                    TextWrapping = TextWrapping.Wrap,
                                    Foreground = Brushes.Black
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
