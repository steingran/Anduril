using Anduril.App.ViewModels;
using Anduril.App.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Animation;
using System.ComponentModel;

namespace Anduril.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private Transitions? _toolInspectorTransitions;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChrome();
        DataContextChanged += OnDataContextChanged;
        MotionPolicy.ReducedMotionChanged += OnReducedMotionChanged;
        Opened += (_, _) => ApplyOverlayState();
        Opened += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
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
        Title = string.Empty;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur
        ];
        ExtendClientAreaToDecorationsHint = true;
        SetEnumPropertyIfPresent("SystemDecorations", "Full");
        TryConfigureAvalonia12Chrome();
    }

    private bool TryConfigureAvalonia12Chrome()
    {
        var windowDecorationsProperty = typeof(Window).GetProperty("WindowDecorations");
        if (windowDecorationsProperty is null || TopBar is null)
            return false;

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

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        MotionPolicy.ReducedMotionChanged -= OnReducedMotionChanged;
        DataContextChanged -= OnDataContextChanged;
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyOverlayState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsToolInspectorOpen) or nameof(MainWindowViewModel.IsSettingsOpen))
            ApplyOverlayState();
    }

    private void OnReducedMotionChanged(object? sender, bool isReducedMotion) =>
        ApplyOverlayState();

    private void ApplyOverlayState()
    {
        if (ToolInspectorPanel is null || ToolsToggleButton is null)
            return;

        _toolInspectorTransitions ??= ToolInspectorPanel.Transitions;
        ToolInspectorPanel.Transitions = MotionPolicy.IsReducedMotion ? null : _toolInspectorTransitions;

        var isToolInspectorOpen = _viewModel?.IsToolInspectorOpen == true;
        ToolsToggleButton.Classes.Set("active", isToolInspectorOpen);
        ToolInspectorPanel.Classes.Set("open", isToolInspectorOpen);
        ToolInspectorPanel.IsVisible = isToolInspectorOpen;
        ToolInspectorPanel.IsHitTestVisible = isToolInspectorOpen;
    }

    private void ApplyResponsiveLayout()
    {
        if (TopBarModelPicker is null || NavigationSegments is null)
            return;

        var compactTopBar = Bounds.Width > 0 && Bounds.Width <= 1120;
        NavigationSegments.Width = compactTopBar ? 160 : 172;
        TopBarModelPicker.Width = compactTopBar ? 160 : 220;
    }
}
