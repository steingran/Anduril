using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Anduril.App.ScreenshotTool;

internal static class ScreenshotRenderer
{
    public static async Task<string> RenderAsync(ScreenshotCliOptions options)
    {
        var outputPath = GetOutputPath(options);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var themeVariant = options.Theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark;

        using var scenario = MainWindowScenarioFactory.Create(options, themeVariant);
        var window = scenario.Window;

        window.Show();
        window.ApplyTemplate();
        await FlushUntilStableAsync();

        using var bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless renderer did not return a frame for the requested window.");
        using (var stream = File.Create(outputPath))
        {
            bitmap.Save(stream);
            stream.Flush();
        }

        window.Close();
        return outputPath;
    }

    private static async Task FlushUntilStableAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    private static string GetOutputPath(ScreenshotCliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
            return Path.GetFullPath(options.OutputPath);

        return Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "artifacts",
            "screenshots",
            $"{options.Scenario}-{options.Theme}-{options.Width}x{options.Height}.png"));
    }
}
