using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ReactiveUI.Avalonia;
using ReactiveUI;

namespace Anduril.App.ScreenshotTool;

internal static class ScreenshotAppEntryPoint
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ScreenshotApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .UseReactiveUI(_ => { });
}

internal sealed class ScreenshotApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Default;

        Styles.Add(new FluentTheme());

        AddStyle("avares://Anduril.App/Styles/Type.axaml");
        AddStyle("avares://Anduril.App/Styles/Buttons.axaml");
        AddStyle("avares://Anduril.App/Styles/Inputs.axaml");
        AddStyle("avares://Anduril.App/Styles/SidebarList.axaml");
        AddStyle("avares://Anduril.App/Styles/SegmentedControl.axaml");
        AddStyle("avares://Anduril.App/Styles/Markdown.axaml");

        AddResource("avares://Anduril.App/Styles/Tokens.axaml");
        AddResource("avares://Anduril.App/Styles/Brushes.axaml");
        AddResource("avares://Anduril.App/Styles/Icons.axaml");
        AddResource("avares://Anduril.App/Styles/Motion.axaml");

        RxSchedulers.MainThreadScheduler = AvaloniaScheduler.Instance;
    }

    private void AddStyle(string source) =>
        Styles.Add(new StyleInclude(new Uri("avares://Anduril.App.ScreenshotTool/"))
        {
            Source = new Uri(source),
        });

    private void AddResource(string source) =>
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Anduril.App.ScreenshotTool/"))
        {
            Source = new Uri(source),
        });
}
