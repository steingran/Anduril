using System.Collections.ObjectModel;
using Anduril.App.Models;
using ReactiveUI;

namespace Anduril.App.ViewModels;

public sealed class ModelSelectorViewModel : ViewModelBase
{
    private ModelOption? _selectedModel;

    public ObservableCollection<ModelOption> Models { get; } = [];

    public ModelOption? SelectedModel
    {
        get => _selectedModel;
        set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
    }
}
