using Avalonia.Threading;

namespace Anduril.App.ScreenshotTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        var options = ScreenshotCliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (options.ListScenarios)
        {
            foreach (var scenario in MainWindowScenarioFactory.GetScenarioNames())
                Console.WriteLine(scenario);

            return 0;
        }

        ScreenshotAppEntryPoint.BuildAvaloniaApp().SetupWithoutStarting();

        string? outputPath = null;
        Exception? failure = null;
        using var cancellation = new CancellationTokenSource();

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                outputPath = await ScreenshotRenderer.RenderAsync(options);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                cancellation.Cancel();
            }
        });

        try
        {
            Dispatcher.UIThread.MainLoop(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }

        if (failure is not null)
            throw failure;

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new InvalidOperationException("Screenshot renderer exited without producing an output path.");

        Console.WriteLine(outputPath);
        Console.Out.Flush();
        Environment.Exit(0);
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
Usage:
  dotnet run --project src/Anduril.App.ScreenshotTool -- --scenario <name> [options]

Options:
  --scenario <name>   Scenario to render. Default: main-window-default
  --theme <light|dark>
  --width <pixels>    Default: 1280
  --height <pixels>   Default: 800
  --output <path>     Default: artifacts/screenshots/<scenario>-<theme>-<width>x<height>.png
  --list              Print supported scenarios
  --help              Print this help text
""");
    }
}

internal sealed record ScreenshotCliOptions(
    string Scenario,
    string Theme,
    int Width,
    int Height,
    string? OutputPath,
    bool ListScenarios,
    bool ShowHelp)
{
    public static ScreenshotCliOptions Parse(string[] args)
    {
        var scenario = "main-window-default";
        var theme = "dark";
        var width = 1280;
        var height = 800;
        string? outputPath = null;
        var list = false;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario":
                    scenario = ReadValue(args, ref i, "--scenario");
                    break;
                case "--theme":
                    theme = ReadValue(args, ref i, "--theme").ToLowerInvariant();
                    break;
                case "--width":
                    width = int.Parse(ReadValue(args, ref i, "--width"));
                    break;
                case "--height":
                    height = int.Parse(ReadValue(args, ref i, "--height"));
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref i, "--output");
                    break;
                case "--list":
                    list = true;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        if (theme is not ("light" or "dark"))
            throw new ArgumentException($"Unsupported theme '{theme}'. Use 'light' or 'dark'.");

        if (width <= 0 || height <= 0)
            throw new ArgumentException("Width and height must be positive integers.");

        return new ScreenshotCliOptions(scenario, theme, width, height, outputPath, list, help);
    }

    private static string ReadValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {flag}.");

        index++;
        return args[index];
    }
}
