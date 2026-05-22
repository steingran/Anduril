using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Anduril.App.Tests;

public sealed class StreamingDotIndicatorTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task ReducedMotion_DisablesAnimatedClasses_AndUsesStaticOpacities()
    {
        await RunOnUIThread(async () =>
        {
            MotionPolicy.IsReducedMotion = true;

            var control = new StreamingDotIndicator
            {
                IsStreaming = true
            };

            var window = new Window { Content = control, Width = 160, Height = 80 };

            try
            {
                window.Show();

                var dots = control.FindDescendant<StackPanel>(panel => panel.Name == "DotsPanel")
                    .Children.OfType<Ellipse>()
                    .ToArray();

                await Assert.That(dots).Count().IsEqualTo(3);
                await Assert.That(dots.All(dot => !dot.Classes.Contains("animate"))).IsTrue();
                await Assert.That(dots[0].Opacity).IsEqualTo(1.0);
                await Assert.That(dots[1].Opacity).IsEqualTo(0.55);
                await Assert.That(dots[2].Opacity).IsEqualTo(0.55);
            }
            finally
            {
                MotionPolicy.IsReducedMotion = false;
                window.Close();
            }
        });
    }
}
