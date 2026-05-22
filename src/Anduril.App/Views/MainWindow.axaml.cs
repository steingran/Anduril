using Anduril.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Anduril.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChromeCompatibility();
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

    private void ConfigureWindowChromeCompatibility()
    {
        SetChromeMode("WindowDecorations", "Full");
        SetChromeMode("SystemDecorations", "Full");
        SetTitleBarRoleIfAvailable(TopBar);
    }

    private void SetChromeMode(string propertyName, string enumValue)
    {
        var property = GetType().BaseType?.GetProperty(propertyName) ?? typeof(Window).GetProperty(propertyName);
        if (property is null || !property.PropertyType.IsEnum)
            return;

        if (!Enum.TryParse(property.PropertyType, enumValue, ignoreCase: false, out var parsed))
            return;

        property.SetValue(this, parsed);
    }

    private static void SetTitleBarRoleIfAvailable(Visual visual)
    {
        var propertyType = Type.GetType("Avalonia.Controls.Chrome.WindowDecorationProperties, Avalonia.Controls");
        var roleType = Type.GetType("Avalonia.Input.WindowDecorationsElementRole, Avalonia.Base");
        if (propertyType is null || roleType is null)
            return;

        if (!Enum.TryParse(roleType, "TitleBar", ignoreCase: false, out var titleBarRole))
            return;

        var setMethod = propertyType.GetMethod("SetElementRole", [typeof(Visual), roleType]);
        setMethod?.Invoke(null, [visual, titleBarRole]);
    }
}
