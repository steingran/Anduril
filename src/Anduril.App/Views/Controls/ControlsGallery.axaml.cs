using System.Collections.ObjectModel;
using System.Windows.Input;
using Anduril.App.Models;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public partial class ControlsGallery : UserControl
{
    public ControlsGallery()
    {
        InitializeComponent();

        Models =
        [
            new ModelOption
            {
                ProviderId = "openai::gpt-4.1",
                DisplayName = "OpenAI: GPT-4.1",
                ModelName = "gpt-4.1",
                IsAvailable = true
            },
            new ModelOption
            {
                ProviderId = "anthropic::claude-sonnet-4",
                DisplayName = "Anthropic: Claude Sonnet 4",
                ModelName = "claude-sonnet-4",
                IsAvailable = true
            }
        ];

        SelectedModel = Models[0];
        DiffLines =
        [
            new DiffLine(DiffLineKind.Context, "@@ -1,4 +1,4 @@"),
            new DiffLine(DiffLineKind.Added, "+ Added a new control sample."),
            new DiffLine(DiffLineKind.Removed, "- Removed stale placeholder text.")
        ];
        ToolCallSample = new ToolCallSummary
        {
            ToolId = "file.search",
            ToolName = "file.search",
            ToolIcon = "🔍",
            Detail = "Searching user workspace for markdown files."
        };
        SamplePath = "src/Anduril.App/Views/Controls/SegmentedControl.axaml";
        SegmentItems = ["Chat", "Code", "Files"];
        NoOpCommand = new DelegateCommand(() => { });
        DataContext = this;
    }

    public IReadOnlyList<string> SegmentItems { get; }

    public ObservableCollection<ModelOption> Models { get; }

    public ObservableCollection<DiffLine> DiffLines { get; }

    public ToolCallSummary ToolCallSample { get; }

    public string SamplePath { get; }

    public ModelOption? SelectedModel { get; set; }

    public ICommand NoOpCommand { get; }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
