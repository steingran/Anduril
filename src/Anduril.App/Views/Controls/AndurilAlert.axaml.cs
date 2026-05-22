using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public enum AndurilAlertVariant
{
    Info,
    Success,
    Warning,
    Danger
}

public partial class AndurilAlert : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AndurilAlert, string>(nameof(Title), "Notice");

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<AndurilAlert, string?>(nameof(Body));

    public static readonly StyledProperty<AndurilAlertVariant> VariantProperty =
        AvaloniaProperty.Register<AndurilAlert, AndurilAlertVariant>(nameof(Variant));

    public static readonly StyledProperty<string?> PrimaryActionLabelProperty =
        AvaloniaProperty.Register<AndurilAlert, string?>(nameof(PrimaryActionLabel));

    public static readonly StyledProperty<ICommand?> PrimaryActionCommandProperty =
        AvaloniaProperty.Register<AndurilAlert, ICommand?>(nameof(PrimaryActionCommand));

    private static readonly DirectProperty<AndurilAlert, bool> HasPrimaryActionProperty =
        AvaloniaProperty.RegisterDirect<AndurilAlert, bool>(
            nameof(HasPrimaryAction),
            alert => alert.HasPrimaryAction);

    private static readonly DirectProperty<AndurilAlert, string> IconGlyphProperty =
        AvaloniaProperty.RegisterDirect<AndurilAlert, string>(
            nameof(IconGlyph),
            alert => alert.IconGlyph);

    public AndurilAlert()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public AndurilAlertVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string? PrimaryActionLabel
    {
        get => GetValue(PrimaryActionLabelProperty);
        set => SetValue(PrimaryActionLabelProperty, value);
    }

    public ICommand? PrimaryActionCommand
    {
        get => GetValue(PrimaryActionCommandProperty);
        set => SetValue(PrimaryActionCommandProperty, value);
    }

    public bool HasPrimaryAction =>
        !string.IsNullOrWhiteSpace(PrimaryActionLabel) && PrimaryActionCommand is not null;

    public string IconGlyph => Variant switch
    {
        AndurilAlertVariant.Success => "✓",
        AndurilAlertVariant.Warning => "!",
        AndurilAlertVariant.Danger => "×",
        _ => "i"
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VariantProperty ||
            change.Property == PrimaryActionLabelProperty ||
            change.Property == PrimaryActionCommandProperty ||
            change.Property == BodyProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (AlertRoot is null)
            return;

        var variantClass = Variant.ToString().ToLowerInvariant();
        ApplyVariantClass(AlertRoot.Classes, variantClass);
        ApplyVariantClass(IconRoot.Classes, variantClass);
        BodyBlock.IsVisible = !string.IsNullOrWhiteSpace(Body);
        PrimaryActionButton.IsVisible = HasPrimaryAction;

        RaisePropertyChanged(HasPrimaryActionProperty, false, HasPrimaryAction);
        RaisePropertyChanged(IconGlyphProperty, string.Empty, IconGlyph);
    }

    private static void ApplyVariantClass(Classes classes, string variantClass)
    {
        classes.Set("info", false);
        classes.Set("success", false);
        classes.Set("warning", false);
        classes.Set("danger", false);
        classes.Set(variantClass, true);
    }
}
