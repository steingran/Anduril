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

                var mark = control.FindDescendant<Image>(image => image.Name == "MarkImage");
                var lightWordmark = control.FindDescendant<Image>(image => image.Name == "LightWordmarkImage");
                var darkWordmark = control.FindDescendant<Image>(image => image.Name == "DarkWordmarkImage");

                await Assert.That(mark.Width).IsEqualTo(36d);
                await Assert.That(lightWordmark).IsNotNull();
                await Assert.That(lightWordmark.IsVisible).IsFalse();
                await Assert.That(darkWordmark).IsNotNull();
                await Assert.That(darkWordmark.IsVisible).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
