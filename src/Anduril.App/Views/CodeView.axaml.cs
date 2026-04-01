using Anduril.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Anduril.App.Views;

public partial class CodeView : UserControl
{
    public CodeView()
    {
        InitializeComponent();
        BrowseRepoButton.Click += OnBrowseRepoClicked;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is CodeViewModel vm)
            vm.Messages.CollectionChanged += (_, _) =>
                Dispatcher.UIThread.Post(() => MessagesScroll.ScrollToEnd());
    }

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
