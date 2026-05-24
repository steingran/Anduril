using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class PathBreadcrumbTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task Path_Splits_Into_OrderedSegments()
    {
        await RunOnUIThread(async () =>
        {
            var control = new PathBreadcrumb
            {
                Path = "src/Anduril.App/Views/Controls/SegmentedControl.axaml"
            };
            var labels = control.Segments.Select(segment => segment.Label).ToArray();

            await Assert.That(labels.Length).IsEqualTo(5);
            await Assert.That(labels[0]).IsEqualTo("src");
            await Assert.That(labels[1]).IsEqualTo("Anduril.App");
            await Assert.That(labels[2]).IsEqualTo("Views");
            await Assert.That(labels[3]).IsEqualTo("Controls");
            await Assert.That(labels[4]).IsEqualTo("SegmentedControl.axaml");
            await Assert.That(control.Segments[0].IsFirst).IsTrue();
            await Assert.That(control.Segments.Skip(1).All(segment => !segment.IsFirst)).IsTrue();
        });
    }

    [Test]
    public async Task EmptyPath_Clears_Segments()
    {
        await RunOnUIThread(async () =>
        {
            var control = new PathBreadcrumb
            {
                Path = "src/Anduril.App"
            };

            await Assert.That(control.Segments.Count).IsEqualTo(2);

            control.Path = string.Empty;

            await Assert.That(control.Segments.Count).IsEqualTo(0);
        });
    }

    [Test]
    public async Task RenderedBreadcrumb_Shows_Separators_For_NonFirst_Segments()
    {
        await RunOnUIThread(async () =>
        {
            var control = new PathBreadcrumb
            {
                Path = "src/Anduril.App/Views"
            };
            var window = new Window { Content = control, Width = 320, Height = 80 };

            try
            {
                window.Show();

                var separators = control.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(text => text.Classes.Contains("breadcrumb-separator"))
                    .ToArray();

                await Assert.That(separators.Length).IsEqualTo(3);
                await Assert.That(separators.Count(text => text.IsVisible)).IsEqualTo(2);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
