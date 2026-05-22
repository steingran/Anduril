using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class AndurilCardTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task HeaderAndFooter_Visibility_FollowsProperties()
    {
        await RunOnUIThread(async () =>
        {
            var control = new AndurilCard
            {
                Title = "Inspector",
                Subtitle = "Latest tool output",
                Footer = new TextBlock { Text = "Footer" },
                Body = new TextBlock { Text = "Body" }
            };

            var window = new Window { Content = control, Width = 320, Height = 180 };

            try
            {
                window.Show();

                var renderedText = control
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(text => text.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                await Assert.That(renderedText).Contains("Inspector");
                await Assert.That(renderedText).Contains("Latest tool output");
                await Assert.That(renderedText).Contains("Body");
                await Assert.That(renderedText).Contains("Footer");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
