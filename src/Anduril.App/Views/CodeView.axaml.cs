using Anduril.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Anduril.App.Views;

public partial class CodeView : UserControl
{
    private CodeViewModel? _subscribedVm;

    public CodeView()
    {
        InitializeComponent();
        BrowseRepoButton.Click += OnBrowseRepoClicked;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedVm is not null)
            _subscribedVm.Messages.CollectionChanged -= OnMessagesChanged;

        _subscribedVm = DataContext as CodeViewModel;

        if (_subscribedVm is not null)
            _subscribedVm.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.UIThread.Post(() => MessagesScroll.ScrollToEnd());

    private async void OnBrowseRepoClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not CodeViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select repository root",
            AllowMultiple = false
        });

        if (folders.Count > 0)
            vm.SelectedRepoPath = folders[0].Path.LocalPath;
    }
}
