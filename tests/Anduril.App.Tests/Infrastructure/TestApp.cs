using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;
using ReactiveUI;

namespace Anduril.App.Tests.Infrastructure;

/// <summary>
/// Minimal Avalonia <see cref="Application"/> used by the headless test session.
/// The real <see cref="Anduril.App.App"/> instantiates <c>SignalRChatService</c> against
/// <c>HostService.BaseUrl</c> in <c>OnFrameworkInitializationCompleted</c> — we must not
/// boot that here. This subclass only loads the FluentTheme so that controls under test
/// resolve their default styles, and pins <see cref="RxApp.MainThreadScheduler"/> to the
/// Avalonia dispatcher so ReactiveCommand's <c>CanExecuteChanged</c> notifications flow
/// through the UI thread instead of the default long-running thread pool scheduler.
/// Without this pin the production app is fine (it calls <c>UseReactiveUI</c> on the
/// AppBuilder), but <see cref="Avalonia.Headless.HeadlessUnitTestSession.StartNew"/>
/// builds its own AppBuilder that never invokes that hook, and view-swapping scenarios
/// (e.g. MainWindow tab navigation) race a <c>Button.CanExecuteChanged</c> onto the Rx
/// LongRunning thread and crash with <c>Call from invalid thread</c>.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
    }
}
