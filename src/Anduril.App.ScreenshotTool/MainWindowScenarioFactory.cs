using Anduril.App.Models;
using Anduril.App.ViewModels;
using Anduril.App.Views;
using Avalonia.Controls;
using Avalonia.Styling;
using System.Collections.ObjectModel;

namespace Anduril.App.ScreenshotTool;

internal static class MainWindowScenarioFactory
{
    public static IReadOnlyList<string> GetScenarioNames() =>
    [
        "main-window-default",
        "main-window-empty",
        "main-window-tool-inspector",
        "main-window-settings",
    ];

    public static ScenarioHandle Create(ScreenshotCliOptions options, ThemeVariant themeVariant)
    {
        var now = DateTimeOffset.Now;
        var viewModel = new MainWindowViewModel(new FakeChatService(), new InMemoryUserPreferencesService());
        SeedModels(viewModel);
        SeedConversations(viewModel, now, options.Scenario == "main-window-empty");

        if (options.Scenario == "main-window-tool-inspector")
        {
            viewModel.ToolInspectorProviders.Add(new ToolInspectorItem
            {
                Name = "Anthropic",
                Description = "Connected",
                IsAvailable = true,
                SupportsChatCompletion = true,
                Functions = ["Chat", "Reasoning"]
            });
            viewModel.ToolInspectorIntegrations.Add(new ToolInspectorItem
            {
                Name = "GitHub",
                Description = "Repository access",
                IsAvailable = true,
                Functions = ["Issues", "PRs", "Files"]
            });
            viewModel.IsToolInspectorOpen = true;
        }

        if (options.Scenario == "main-window-settings")
            viewModel.IsSettingsOpen = true;

        var window = new MainWindow
        {
            Width = options.Width,
            Height = options.Height,
            RequestedThemeVariant = themeVariant,
            DataContext = viewModel
        };

        return new ScenarioHandle(window);
    }

    private static void SeedModels(MainWindowViewModel viewModel)
    {
        viewModel.AvailableModels.Clear();
        viewModel.AvailableModels.Add(new ModelOption
        {
            ProviderId = "anthropic::claude-sonnet-4",
            DisplayName = "Anthropic: Claude Sonnet 4",
            ModelName = "claude-sonnet-4",
            IsAvailable = true
        });
        viewModel.AvailableModels.Add(new ModelOption
        {
            ProviderId = "openai::gpt-5",
            DisplayName = "OpenAI: GPT-5",
            ModelName = "gpt-5",
            IsAvailable = true
        });
        viewModel.SelectedModel = viewModel.AvailableModels[0];
    }

    private static void SeedConversations(MainWindowViewModel viewModel, DateTimeOffset now, bool empty)
    {
        viewModel.Conversations.Clear();
        viewModel.GroupedConversations.Clear();

        if (empty)
        {
            viewModel.SelectedConversation = null;
            return;
        }

        var entries = new[]
        {
            new ConversationEntry
            {
                ChatConversationId = "today-chat",
                CodeConversationId = "today-code",
                Title = "Mission planning notes",
                CreatedAt = now
            },
            new ConversationEntry
            {
                ChatConversationId = "yesterday-chat",
                CodeConversationId = "yesterday-code",
                Title = "Telemetry cleanup",
                CreatedAt = now.AddDays(-1)
            },
            new ConversationEntry
            {
                ChatConversationId = "week-chat",
                CodeConversationId = "week-code",
                Title = "Provider failover",
                CreatedAt = now.AddDays(-3)
            },
            new ConversationEntry
            {
                ChatConversationId = "earlier-chat",
                CodeConversationId = "earlier-code",
                Title = "Desktop shell draft",
                CreatedAt = now.AddDays(-12)
            }
        };

        foreach (var entry in entries)
            viewModel.Conversations.Add(entry);

        viewModel.GroupedConversations.Add(new ConversationGroup("Today", new ObservableCollection<ConversationEntry>([entries[0]])));
        viewModel.GroupedConversations.Add(new ConversationGroup("Yesterday", new ObservableCollection<ConversationEntry>([entries[1]])));
        viewModel.GroupedConversations.Add(new ConversationGroup("Last 7 days", new ObservableCollection<ConversationEntry>([entries[2]])));
        viewModel.GroupedConversations.Add(new ConversationGroup("Earlier", new ObservableCollection<ConversationEntry>([entries[3]])));
        viewModel.SelectedConversation = entries[0];
    }
}

internal sealed class ScenarioHandle : IDisposable
{
    public ScenarioHandle(Window window) => Window = window;

    public Window Window { get; }

    public void Dispose()
    {
        if (Window.IsVisible)
            Window.Close();
    }
}
