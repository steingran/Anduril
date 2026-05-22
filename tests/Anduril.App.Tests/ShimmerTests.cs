using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class ShimmerTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task ReducedMotion_DisablesAnimationClasses()
    {
        await RunOnUIThread(async () =>
        {
            MotionPolicy.IsReducedMotion = true;
            var control = new Shimmer
            {
                LineCount = 3,
                MaxLineWidth = 180,
                LineHeight = 12
            };
            var window = new Window { Content = control, Width = 300, Height = 120 };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var lines = control.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
                    .Where(border => border.Classes.Contains("shimmer-line"))
                    .ToArray();

                await Assert.That(lines.Length).IsEqualTo(3);
                await Assert.That(lines.All(line => line.Classes.Contains("reduced"))).IsTrue();
                await Assert.That(lines.All(line => !line.Classes.Contains("animated"))).IsTrue();
            }
            finally
            {
                MotionPolicy.IsReducedMotion = false;
                window.Close();
            }
        });
    }

    [Test]
    public async Task LineCount_BuildsExpectedWidths()
    {
        await RunOnUIThread(async () =>
        {
            var control = new Shimmer { LineCount = 2 };
            var window = new Window { Content = control, Width = 300, Height = 120 };

            try
            {
                window.Show();
                await Assert.That(control.Lines.Count).IsEqualTo(2);
                await Assert.That(control.Lines[0]).IsEqualTo(control.MaxLineWidth);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
