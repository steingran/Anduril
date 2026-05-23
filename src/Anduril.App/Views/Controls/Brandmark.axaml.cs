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
        if (MarkHost is null ||
            MarkPrimaryStroke is null ||
            MarkAccentStroke is null ||
            WordmarkText is null)
            return;

        var (markSize, wordmarkSize, spacing, padding) = Size switch
        {
            BrandmarkSize.Small => (20d, 18d, 8d, new Thickness(0, 1, 0, 0)),
            BrandmarkSize.Large => (36d, 28d, 14d, new Thickness(0, 1, 0, 0)),
            _ => (28d, 22d, 10d, new Thickness(0, 1, 0, 0))
        };

        MarkHost.Width = markSize;
        MarkHost.Height = markSize;
        LayoutRoot.Spacing = spacing;
        WordmarkText.FontSize = wordmarkSize;
        WordmarkText.Padding = padding;
        WordmarkText.FontFamily = FontFamily.Parse("avares://Avalonia.Fonts.Inter/Assets#Inter, $Default");

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var markBrush = isDark
            ? new SolidColorBrush(Color.Parse("#E7EEF8"))
            : new SolidColorBrush(Color.Parse("#0F172A"));

        MarkPrimaryStroke.Stroke = markBrush;
        MarkAccentStroke.Stroke = markBrush;
        WordmarkText.Foreground = isDark
            ? new SolidColorBrush(Color.Parse("#F8FAFC"))
            : new SolidColorBrush(Color.Parse("#0F172A"));
    }
}
