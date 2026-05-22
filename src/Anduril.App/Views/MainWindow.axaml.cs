using Anduril.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Anduril.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
            return;

        if (!viewModel.IsOverlayOpen)
            return;

        viewModel.CloseTransientPanelsCommand.Execute().Subscribe();
        e.Handled = true;
    }

    private void OnRenameConversationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ConversationEntry conversation } ||
            DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.RenameConversationCommand.Execute(conversation).Subscribe();
    }

    private void OnDeleteConversationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ConversationEntry conversation } ||
            DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.DeleteConversationCommand.Execute(conversation).Subscribe();
    }
}
