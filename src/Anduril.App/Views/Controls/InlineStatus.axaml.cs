using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public enum InlineStatusVariant
{
    Info,
    Stopped,
    Cancelled,
    Failed
}

/// <summary>
/// Reusable inline status pill with a variant, label, and optional trailing action.
/// </summary>
public partial class InlineStatus : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<InlineStatus, string>(nameof(Text), "Info");

    public static readonly StyledProperty<InlineStatusVariant> VariantProperty =
        AvaloniaProperty.Register<InlineStatus, InlineStatusVariant>(nameof(Variant));

    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<InlineStatus, string?>(nameof(ActionLabel));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<InlineStatus, ICommand?>(nameof(ActionCommand));

    public InlineStatus()
    {
        InitializeComponent();
        UpdateVariantClasses();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public InlineStatusVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string? ActionLabel
    {
        get => GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel) && ActionCommand is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VariantProperty)
            UpdateVariantClasses();

        if (change.Property == ActionLabelProperty || change.Property == ActionCommandProperty)
            RaisePropertyChanged(HasActionProperty, GetHasAction(change, oldValue: true), GetHasAction(change, oldValue: false));
    }

    private static readonly DirectProperty<InlineStatus, bool> HasActionProperty =
        AvaloniaProperty.RegisterDirect<InlineStatus, bool>(
            nameof(HasAction),
            inlineStatus => inlineStatus.HasAction);

    private bool GetHasAction(AvaloniaPropertyChangedEventArgs change, bool oldValue)
    {
        var actionLabel = change.Property == ActionLabelProperty
            ? oldValue ? change.GetOldValue<string?>() : change.GetNewValue<string?>()
            : ActionLabel;
        var actionCommand = change.Property == ActionCommandProperty
            ? oldValue ? change.GetOldValue<ICommand?>() : change.GetNewValue<ICommand?>()
            : ActionCommand;

        return !string.IsNullOrWhiteSpace(actionLabel) && actionCommand is not null;
    }

    private void UpdateVariantClasses()
    {
        if (StatusRoot is null || StatusDot is null || StatusLabel is null)
            return;

        var variantClass = Variant.ToString().ToLowerInvariant();
        ApplyVariantClass(StatusRoot.Classes, variantClass);
        ApplyVariantClass(StatusDot.Classes, variantClass);
        ApplyVariantClass(StatusLabel.Classes, variantClass);
    }

    private static void ApplyVariantClass(Classes classes, string variantClass)
    {
        classes.Set("info", false);
        classes.Set("stopped", false);
        classes.Set("cancelled", false);
        classes.Set("failed", false);
        classes.Set(variantClass, true);
    }
}
