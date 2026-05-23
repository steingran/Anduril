using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Anduril.App.Tests;

public sealed class BrandmarkTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task SizeAndTheme_UpdateRenderedAssets()
    {
        await RunOnUIThread(async () =>
        {
            var control = new Brandmark { Size = BrandmarkSize.Large };
            var window = new Window
            {
                RequestedThemeVariant = ThemeVariant.Dark,
                Content = control,
                Width = 260,
                Height = 100
            };

            try
            {
                window.Show();

                var mark = control.FindDescendant<Border>(border => border.Name == "MarkBadge");
                var glyph = control.FindDescendant<TextBlock>(text => text.Name == "MarkGlyph");
                var wordmark = control.FindDescendant<TextBlock>(text => text.Name == "WordmarkText");

                await Assert.That(mark.Width).IsEqualTo(36d);
                await Assert.That(glyph.Text).IsEqualTo("A");
                await Assert.That(wordmark.Text).IsEqualTo("Anduril");
                await Assert.That(wordmark.FontSize).IsEqualTo(28d);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
