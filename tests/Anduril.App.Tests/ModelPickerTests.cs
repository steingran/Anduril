using Anduril.App.Models;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System.Linq;
using Avalonia.VisualTree;

namespace Anduril.App.Tests;

public sealed class ModelPickerTests : AvaloniaHeadlessTestBase
{
    [Test]
    public async Task SelectedModel_ShowsInHeaderAndPopupGroups()
    {
        await RunOnUIThread(async () =>
        {
            var selected = new ModelOption
            {
                ProviderId = "openai::gpt-4o",
                DisplayName = "OpenAI: GPT-4o",
                ModelName = "gpt-4o",
                IsAvailable = true
            };

            var unavailable = new ModelOption
            {
                ProviderId = "anthropic::claude-sonnet",
                DisplayName = "Anthropic: Claude",
                ModelName = "claude-sonnet",
                IsAvailable = false
            };

            var control = new ModelPicker
            {
                ItemsSource = new[] { selected, unavailable },
                SelectedModel = selected,
                Width = 360
            };

            var window = new Window { Content = control, Width = 400, Height = 240 };

            try
            {
                window.Show();
                await Dispatcher.UIThread.InvokeAsync(() => { });

                var selectedModelText = control.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .FirstOrDefault(text => text.Name == "SelectedModelText");

                await Assert.That(selectedModelText?.Text).IsEqualTo("OpenAI: GPT-4o");
                await Assert.That(control.PickerItems.Count).IsEqualTo(4);
                await Assert.That(control.PickerItems.Count(i => i.IsProviderHeader)).IsEqualTo(2);

                var popup = control.GetVisualDescendants()
                    .OfType<Popup>()
                    .FirstOrDefault(element => element.Name == "ModelPopup");

                if (popup is null)
                {
                    Assert.Fail("Expected model picker popup to exist.");
                    return;
                }

                popup.IsOpen = true;
                await Dispatcher.UIThread.InvokeAsync(() => { });

                await Assert.That(popup.IsOpen).IsTrue();
                await Assert.That(control.PickerItems.Any(item => item.AvailabilityLabel == "Unavailable")).IsTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
