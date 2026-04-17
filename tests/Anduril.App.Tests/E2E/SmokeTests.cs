using Anduril.App.Tests.Infrastructure;
using Avalonia.Controls;

namespace Anduril.App.Tests.E2E;

/// <summary>
/// Minimal smoke test that proves the Avalonia.Headless + TUnit plumbing in
/// <see cref="AvaloniaHeadlessTestBase"/> actually boots a window, runs layout,
/// and lets tests dispatch onto the UI thread. Must stay green before adding more
/// interaction tests on top of this infrastructure.
/// </summary>
public sealed class SmokeTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task Window_WithTextBlock_IsVisibleAndMeasured()
    {
        await RunOnUIThread(async () =>
        {
            var textBlock = new TextBlock { Text = "hello headless" };
            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = textBlock,
            };

            window.Show();

            await Assert.That(window.IsVisible).IsTrue();
            await Assert.That(window.Bounds.Width).IsGreaterThan(0);
            await Assert.That(window.Bounds.Height).IsGreaterThan(0);
            await Assert.That(textBlock.Bounds.Width).IsGreaterThan(0);
            await Assert.That(textBlock.Bounds.Height).IsGreaterThan(0);

            window.Close();
        });
    }
}
