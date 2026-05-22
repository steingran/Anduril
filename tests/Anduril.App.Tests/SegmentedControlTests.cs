using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Anduril.App.Tests;

public sealed class SegmentedControlTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task SelectedIndex_BindsThroughHostListBox()
    {
        await RunOnUIThread(async () =>
        {
            var control = new SegmentedControl
            {
                ItemsSource = new[] { "Chat", "Code", "Search" },
                SelectedIndex = 1
            };

            var window = new Window { Content = control, Width = 320, Height = 64 };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var list = control.FindDescendant<ListBox>(listBox => listBox.Name == "Segments");
                await Assert.That(list.SelectedIndex).IsEqualTo(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task ArrowKeys_CycleSelection()
    {
        await RunOnUIThread(async () =>
        {
            var control = new SegmentedControl
            {
                ItemsSource = new[] { "A", "B", "C" },
                SelectedIndex = 2
            };

            var window = new Window { Content = control, Width = 320, Height = 64 };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var list = control.FindDescendant<ListBox>(listBox => listBox.Name == "Segments");
                var selectedContainer = list.ContainerFromIndex(2);
                if (selectedContainer is null)
                {
                    Assert.Fail("Expected selected segmented item container to exist.");
                    return;
                }

                selectedContainer.Focus();
                Dispatcher.UIThread.RunJobs();

                window.PressKey(PhysicalKey.ArrowRight);
                Dispatcher.UIThread.RunJobs();
                await Assert.That(list.SelectedIndex).IsEqualTo(0);

                window.PressKey(PhysicalKey.ArrowLeft);
                Dispatcher.UIThread.RunJobs();
                await Assert.That(list.SelectedIndex).IsEqualTo(2);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
