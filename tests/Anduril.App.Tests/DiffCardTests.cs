using Anduril.App.Models;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class DiffCardTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task DiffCard_PresentsPathAndLines()
    {
        await RunOnUIThread(async () =>
        {
            var control = new DiffCard
            {
                FilePath = "src/Code.cs",
                Kind = StagedActionKind.Edit,
                DiffLines = new[]
                {
                    new DiffLine(DiffLineKind.Context, "@@ -1,2 +1,2 @@", 1, 1),
                    new DiffLine(DiffLineKind.Added, "added line", null, 2)
                }
            };

            var window = new Window { Content = control, Width = 360, Height = 200 };

            try
            {
                window.Show();

                var pathText = control
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .FirstOrDefault(text => text.Name == "DiffPath")?.Text;

                await Assert.That(pathText).IsEqualTo("src/Code.cs");
                await Assert.That(control.DiffKindLabel).IsEqualTo("EDIT");

                var lineItems = control
                    .GetVisualDescendants()
                    .OfType<ItemsControl>()
                    .FirstOrDefault(items => items.Items?.Count > 0);

                await Assert.That(lineItems?.Items?.Count).IsEqualTo(2);

                var lineNumbers = control
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(text => text.Classes.Contains("diff-line-number"))
                    .Select(text => text.Text)
                    .ToArray();

                await Assert.That(lineNumbers).Contains("1");
                await Assert.That(lineNumbers).Contains("2");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Test]
    public async Task DiffCard_AcceptAndRejectButtons_InvokeCommandsWithActionParameter()
    {
        await RunOnUIThread(async () =>
        {
            StagedActionModel? accepted = null;
            StagedActionModel? rejected = null;
            var action = new StagedActionModel
            {
                FilePath = "src/Code.cs",
                Kind = StagedActionKind.Edit,
                DiffLines = [new DiffLine(DiffLineKind.Context, "line", 1, 1)]
            };

            var control = new DiffCard
            {
                DataContext = action,
                FilePath = action.FilePath,
                Kind = action.Kind,
                DiffLines = action.VisibleDiffLines,
                AcceptCommand = ReactiveUI.ReactiveCommand.Create<StagedActionModel>(model => accepted = model),
                RejectCommand = ReactiveUI.ReactiveCommand.Create<StagedActionModel>(model => rejected = model)
            };

            var window = new Window { Content = control, Width = 360, Height = 200 };

            try
            {
                window.Show();

                var buttons = control
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .ToArray();

                var acceptButton = buttons.Single(button => Equals(button.Content, "Accept"));
                var rejectButton = buttons.Single(button => Equals(button.Content, "Reject"));

                acceptButton.Command?.Execute(acceptButton.CommandParameter);
                rejectButton.Command?.Execute(rejectButton.CommandParameter);

                await Assert.That(accepted).IsSameReferenceAs(action);
                await Assert.That(rejected).IsSameReferenceAs(action);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
