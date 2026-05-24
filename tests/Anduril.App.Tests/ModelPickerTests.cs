using Anduril.App.Models;
using Anduril.App.Tests.Infrastructure;
using Anduril.App.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Windows.Input;
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

    [Test]
    public async Task ConfigureButton_Hides_WhenConfigureCommandIsRemoved()
    {
        await RunOnUIThread(async () =>
        {
            var control = new ModelPicker
            {
                ConfigureLabel = "Manage providers",
                ConfigureCommand = new NoOpCommand()
            };

            await Assert.That(control.HasConfigureAction).IsTrue();

            control.ConfigureCommand = null;
            await Dispatcher.UIThread.InvokeAsync(() => { });

            await Assert.That(control.HasConfigureAction).IsFalse();
        });
    }

    [Test]
    public async Task Click_OpenPopup_AfterItemsLoad_ShowsOptionsAndAllowsSelection()
    {
        await RunOnUIThread(async () =>
        {
            var models = new ObservableCollection<ModelOption>();
            var first = new ModelOption
            {
                ProviderId = "openai::gpt-4o",
                DisplayName = "OpenAI: GPT-4o",
                ModelName = "gpt-4o",
                IsAvailable = true
            };
            var second = new ModelOption
            {
                ProviderId = "anthropic::claude-sonnet",
                DisplayName = "Anthropic: Claude Sonnet",
                ModelName = "claude-sonnet",
                IsAvailable = true
            };

            var control = new ModelPicker
            {
                ItemsSource = models,
                Width = 360
            };

            var window = new Window { Content = control, Width = 480, Height = 320 };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                models.Add(first);
                models.Add(second);
                Dispatcher.UIThread.RunJobs();

                await Assert.That(control.PickerItems.Count(item => item.IsModelOption)).IsEqualTo(2);

                var openButton = control.FindDescendant<Button>(button => button.Name == "OpenButton");
                window.ClickCenterOf(openButton);
                await Dispatcher.UIThread.InvokeAsync(() => { });

                var popup = control.FindDescendant<Popup>(element => element.Name == "ModelPopup");
                await Assert.That(popup.IsOpen).IsTrue();

                var optionButtons = popup.Child!
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Where(button => button.Name == "ModelButton")
                    .ToList();

                await Assert.That(optionButtons.Count).IsEqualTo(2);

                window.ClickCenterOf(optionButtons[1]);
                await Dispatcher.UIThread.InvokeAsync(() => { });

                await Assert.That(control.SelectedModel?.ProviderId).IsEqualTo(second.ProviderId);
                await Assert.That(popup.IsOpen).IsFalse();
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
