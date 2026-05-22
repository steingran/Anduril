using System.Windows.Input;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Anduril.App.Tests;

public sealed class InlineStatusTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task Variant_Stopped_AppliesStoppedClasses()
    {
        await RunOnUIThread(async () =>
        {
            var control = new InlineStatus
            {
                Text = "Stopped",
                Variant = InlineStatusVariant.Stopped
            };

            var window = new Window { Content = control, Width = 240, Height = 80 };

            try
            {
                window.Show();

                var label = control.FindDescendant<TextBlock>(text => text.Classes.Contains("status-label"));
                var dot = control.FindDescendant<Ellipse>(ellipse => ellipse.Classes.Contains("status-dot"));

                await Assert.That(label.Classes.Contains("stopped")).IsTrue();
                await Assert.That(dot.Classes.Contains("stopped")).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ActionButton_IsVisible_WhenLabelAndCommandProvided()
    {
        await RunOnUIThread(async () =>
        {
            var control = new InlineStatus
            {
                Text = "Failed",
                Variant = InlineStatusVariant.Failed,
                ActionLabel = "Retry",
                ActionCommand = new NoOpCommand()
            };

            var window = new Window { Content = control, Width = 240, Height = 80 };

            try
            {
                window.Show();

                var button = control.FindDescendant<Button>(b => b.Classes.Contains("status-action"));
                await Assert.That(button.IsVisible).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class NoOpCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) { }
    }
}
