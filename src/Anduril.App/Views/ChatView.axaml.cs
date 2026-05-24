using Anduril.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Anduril.App.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatViewModel viewModel || !viewModel.IsSlashCommandMenuOpen)
            return;

        if (SlashSuggestionsList.ItemCount == 0)
            return;

        var currentIndex = SlashSuggestionsList.SelectedIndex < 0 ? 0 : SlashSuggestionsList.SelectedIndex;

        switch (e.Key)
        {
            case Key.Down:
                SlashSuggestionsList.SelectedIndex = Math.Min(currentIndex + 1, SlashSuggestionsList.ItemCount - 1);
                e.Handled = true;
                break;
            case Key.Up:
                SlashSuggestionsList.SelectedIndex = Math.Max(currentIndex - 1, 0);
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                viewModel.ApplySelectedSlashCommand();
                e.Handled = true;
                break;
            case Key.Escape:
                viewModel.InputText = string.Empty;
                e.Handled = true;
                break;
        }
    }
}
