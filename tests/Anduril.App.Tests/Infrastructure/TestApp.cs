using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;
using ReactiveUI;

namespace Anduril.App.Tests.Infrastructure;

/// <summary>
/// Minimal Avalonia <see cref="Application"/> used by the headless test session.
/// The real <see cref="Anduril.App.App"/> instantiates <c>SignalRChatService</c> against
/// <c>HostService.BaseUrl</c> in <c>OnFrameworkInitializationCompleted</c> — we must not
/// boot that here. This subclass loads the same design-system resources that
/// <c>App.axaml</c> wires up so brush/style lookups behave like production, and pins
/// <see cref="RxApp.MainThreadScheduler"/> to the Avalonia dispatcher so
/// ReactiveCommand's <c>CanExecuteChanged</c> notifications flow through the UI thread
/// instead of the default long-running thread pool scheduler. Without this pin the
/// production app is fine (it calls <c>UseReactiveUI</c> on the AppBuilder), but
/// <see cref="Avalonia.Headless.HeadlessUnitTestSession.StartNew"/> builds its own
/// AppBuilder that never invokes that hook, and view-swapping scenarios (e.g. MainWindow
/// tab navigation) race a <c>Button.CanExecuteChanged</c> onto the Rx LongRunning thread
/// and crash with <c>Call from invalid thread</c>.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Type.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Buttons.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Inputs.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/SidebarList.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/SegmentedControl.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Markdown.axaml"),
        });

        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Tokens.axaml"),
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Brushes.axaml"),
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Anduril.App.Tests/"))
        {
            Source = new Uri("avares://Anduril.App/Styles/Motion.axaml"),
        });

        RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
    }
}
