using Anduril.App.Services;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Anduril.App;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var chatService  = new SignalRChatService(HostService.BaseUrl);
            var prefsService = new UserPreferencesService();
            var mainVm       = new MainWindowViewModel(chatService, prefsService);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            desktop.ShutdownRequested += (_, _) =>
            {
                _ = chatService.DisposeAsync().AsTask();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
