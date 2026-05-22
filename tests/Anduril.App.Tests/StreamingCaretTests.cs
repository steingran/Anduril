using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Anduril.App.Tests;

public sealed class StreamingCaretTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task StreamingCaret_Reflects_IsStreaming_State()
    {
        await RunOnUIThread(async () =>
        {
            var control = new StreamingCaret();
            var window = new Window { Content = control, Width = 40, Height = 40 };

            try
            {
                window.Show();

                var caret = control.FindDescendant<Rectangle>(rectangle => rectangle.Name == "Caret");
                await Assert.That(caret.IsVisible).IsFalse();

                control.IsStreaming = true;
                await Assert.That(caret.IsVisible).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ReducedMotion_Uses_StaticCaret()
    {
        await RunOnUIThread(async () =>
        {
            MotionPolicy.IsReducedMotion = true;

            var control = new StreamingCaret { IsStreaming = true };
            var window = new Window { Content = control, Width = 40, Height = 40 };

            try
            {
                window.Show();

                var caret = control.FindDescendant<Rectangle>(rectangle => rectangle.Name == "Caret");
                await Assert.That(caret.Classes.Contains("blink")).IsFalse();
            }
            finally
            {
                MotionPolicy.IsReducedMotion = false;
                window.Close();
            }
        });
    }
}
