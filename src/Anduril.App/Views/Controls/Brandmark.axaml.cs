using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
        if (MarkBadge is null || MarkGlyph is null || WordmarkText is null)
            return;

        var (markSize, glyphSize, wordmarkSize, spacing, padding) = Size switch
        {
            BrandmarkSize.Small => (20d, 11d, 18d, 8d, new Thickness(0, 1, 0, 0)),
            BrandmarkSize.Large => (36d, 18d, 28d, 14d, new Thickness(0, 1, 0, 0)),
            _ => (28d, 14d, 22d, 10d, new Thickness(0, 1, 0, 0))
        };

        MarkBadge.Width = markSize;
        MarkBadge.Height = markSize;
        LayoutRoot.Spacing = spacing;
        MarkGlyph.FontSize = glyphSize;
        WordmarkText.FontSize = wordmarkSize;
        WordmarkText.Padding = padding;

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        MarkBadge.Background = isDark
            ? new SolidColorBrush(Color.Parse("#E7EEF8"))
            : new SolidColorBrush(Color.Parse("#0F172A"));
        MarkGlyph.Foreground = isDark
            ? new SolidColorBrush(Color.Parse("#0F172A"))
            : new SolidColorBrush(Color.Parse("#F8FAFC"));
        WordmarkText.Foreground = isDark
            ? new SolidColorBrush(Color.Parse("#F8FAFC"))
            : new SolidColorBrush(Color.Parse("#0F172A"));
    }
}
