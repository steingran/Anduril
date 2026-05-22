using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class ControlsGalleryTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task Gallery_Loads_WithTwoThemeScopes()
    {
        await RunOnUIThread(async () =>
        {
            var control = new ControlsGallery();
            var window = new Window
            {
                Content = control,
                Width = 1200,
                Height = 900
            };

            try
            {
                window.Show();

                var cards = control.FindDescendant<Grid>(_ => true);
                var alerts = control.GetVisualDescendants().OfType<AndurilAlert>().ToArray();
                var brandmarks = control.GetVisualDescendants().OfType<Brandmark>().ToArray();

                await Assert.That(cards.Bounds.Width).IsGreaterThan(0);
                await Assert.That(alerts.Length).IsEqualTo(2);
                await Assert.That(brandmarks.Length).IsEqualTo(2);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
