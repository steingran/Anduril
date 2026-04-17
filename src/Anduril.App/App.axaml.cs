using Anduril.App.Services;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

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
            ChatService     = new SignalRChatService(HostService.BaseUrl);
            var prefsService = new UserPreferencesService();
            var mainVm       = new MainWindowViewModel(ChatService, prefsService);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
