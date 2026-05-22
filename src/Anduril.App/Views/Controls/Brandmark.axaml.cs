using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Anduril.App.Views.Controls;

public enum BrandmarkSize
{
    Small,
    Medium,
    Large
}

public partial class Brandmark : UserControl
{
    public static readonly StyledProperty<BrandmarkSize> SizeProperty =
        AvaloniaProperty.Register<Brandmark, BrandmarkSize>(nameof(Size), BrandmarkSize.Medium);

    public Brandmark()
    {
        InitializeComponent();
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        UpdateVisualState();
    }

    public BrandmarkSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SizeProperty)
            UpdateVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) =>
        UpdateVisualState();

    private void UpdateVisualState()
    {
        if (MarkImage is null)
            return;

        var (markSize, wordmarkWidth, wordmarkHeight, spacing) = Size switch
        {
            BrandmarkSize.Small => (20d, 88d, 14d, 8d),
            BrandmarkSize.Large => (36d, 156d, 24d, 14d),
            _ => (28d, 122d, 19d, 10d)
        };

        MarkImage.Width = markSize;
        MarkImage.Height = markSize;
        WordmarkHost.Width = wordmarkWidth;
        WordmarkHost.Height = wordmarkHeight;
        LayoutRoot.Spacing = spacing;

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        LightWordmarkImage.IsVisible = !isDark;
        DarkWordmarkImage.IsVisible = isDark;
    }
}
