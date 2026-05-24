using System.Windows.Input;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;

namespace Anduril.App.Tests;

public sealed class AndurilAlertTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task WarningVariant_ShowsActionAndGlyph()
    {
        await RunOnUIThread(async () =>
        {
            var control = new AndurilAlert
            {
                Title = "Provider missing",
                Body = "Configure one to continue.",
                Variant = AndurilAlertVariant.Warning,
                PrimaryActionLabel = "Configure",
                PrimaryActionCommand = new NoOpCommand()
            };

            var window = new Window { Content = control, Width = 360, Height = 160 };

            try
            {
                window.Show();

                var actionButton = control.FindDescendant<Button>(button => button.Name == "PrimaryActionButton");
                var iconGlyph = control.FindDescendant<TextBlock>(text => text.Name == "IconGlyphBlock");

                await Assert.That(actionButton.IsVisible).IsTrue();
                await Assert.That(iconGlyph.Text).IsEqualTo("!");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task PrimaryAction_Hides_WhenCommandIsRemoved()
    {
        await RunOnUIThread(async () =>
        {
            var control = new AndurilAlert
            {
                Title = "Provider missing",
                Body = "Configure one to continue.",
                Variant = AndurilAlertVariant.Warning,
                PrimaryActionLabel = "Configure",
                PrimaryActionCommand = new NoOpCommand()
            };

            var window = new Window { Content = control, Width = 360, Height = 160 };

            try
            {
                window.Show();

                var actionButton = control.FindDescendant<Button>(button => button.Name == "PrimaryActionButton");
                await Assert.That(actionButton.IsVisible).IsTrue();

                control.PrimaryActionCommand = null;

                await Assert.That(actionButton.IsVisible).IsFalse();
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
