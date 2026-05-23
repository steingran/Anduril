using Anduril.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Anduril.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChrome();
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

    private void ConfigureWindowChrome()
    {
        if (TryConfigureAvalonia12Chrome())
            return;

        // Avalonia 11 fallback: keep the startup surface fully opaque.
        Title = string.Empty;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.None
        ];
        ExtendClientAreaToDecorationsHint = false;
        SetEnumPropertyIfPresent("SystemDecorations", "Full");
    }

    private bool TryConfigureAvalonia12Chrome()
    {
        var windowDecorationsProperty = typeof(Window).GetProperty("WindowDecorations");
        if (windowDecorationsProperty is null || TopBar is null)
            return false;

        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur
        ];
        ExtendClientAreaToDecorationsHint = true;

        var decorationsValue = Enum.Parse(windowDecorationsProperty.PropertyType, "Full", ignoreCase: false);
        windowDecorationsProperty.SetValue(this, decorationsValue);

        var decorationPropertiesType = Type.GetType("Avalonia.Controls.Chrome.WindowDecorationProperties, Avalonia.Controls");
        var elementRoleType = Type.GetType("Avalonia.Input.WindowDecorationsElementRole, Avalonia.Base");
        var setElementRole = decorationPropertiesType?
            .GetMethods()
            .FirstOrDefault(method =>
            {
                if (method.Name != "SetElementRole")
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 2 && parameters[1].ParameterType == elementRoleType;
            });

        if (elementRoleType is not null && setElementRole is not null)
        {
            var titleBarRole = Enum.Parse(elementRoleType, "TitleBar", ignoreCase: false);
            setElementRole.Invoke(null, [TopBar, titleBarRole]);
        }

        return true;
    }

    private void SetEnumPropertyIfPresent(string propertyName, string enumValue)
    {
        var property = typeof(Window).GetProperty(propertyName);
        if (property is null || !property.PropertyType.IsEnum)
            return;

        var parsed = Enum.Parse(property.PropertyType, enumValue, ignoreCase: false);
        property.SetValue(this, parsed);
    }
}
