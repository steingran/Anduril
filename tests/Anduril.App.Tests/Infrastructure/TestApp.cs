using Avalonia;
using Avalonia.Themes.Fluent;

namespace Anduril.App.Tests.Infrastructure;

/// <summary>
/// Minimal Avalonia <see cref="Application"/> used by the headless test session.
/// The real <see cref="Anduril.App.App"/> instantiates <c>SignalRChatService</c> against
/// <c>HostService.BaseUrl</c> in <c>OnFrameworkInitializationCompleted</c> — we must not
/// boot that here. This subclass only loads the FluentTheme so that controls under test
/// resolve their default styles.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
