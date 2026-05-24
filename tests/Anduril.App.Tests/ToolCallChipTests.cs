using Anduril.App.Models;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace Anduril.App.Tests;

public sealed class ToolCallChipTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task Detail_Visibility_TracksExpandedState()
    {
        await RunOnUIThread(async () =>
        {
            var control = new ToolCallChip
            {
                Summary = new ToolCallSummary
                {
                    ToolName = "search",
                    ToolId = "search",
                    ToolIcon = "🔍",
                    Detail = "Searching content."
                }
            };

            var window = new Window { Content = control, Width = 420, Height = 120 };

            try
            {
                window.Show();

                var detail = control.GetVisualDescendants().OfType<TextBlock>()
                    .FirstOrDefault(block => block.Name == "ToolDetail");

                await Assert.That(detail!.IsVisible).IsFalse();

                control.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                await Assert.That(detail.IsVisible).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
