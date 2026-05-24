using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Anduril.App.Views.Controls;

public partial class Shimmer : UserControl
{
    public static readonly StyledProperty<int> LineCountProperty =
        AvaloniaProperty.Register<Shimmer, int>(nameof(LineCount), 4);

    public static readonly StyledProperty<double> MinLineWidthProperty =
        AvaloniaProperty.Register<Shimmer, double>(nameof(MinLineWidth), 70);

    public static readonly StyledProperty<double> MaxLineWidthProperty =
        AvaloniaProperty.Register<Shimmer, double>(nameof(MaxLineWidth), 100);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<Shimmer, double>(nameof(LineHeight), 14);

    private readonly ObservableCollection<double> _lineWidths = [];

    public Shimmer()
    {
        InitializeComponent();
        MotionPolicy.ReducedMotionChanged += OnReducedMotionChanged;
        Loaded += (_, _) => ApplyMotionMode(MotionPolicy.IsReducedMotion);
        BuildLines();
        ApplyMotionMode(MotionPolicy.IsReducedMotion);
    }

    public int LineCount
    {
        get => GetValue(LineCountProperty);
        set => SetValue(LineCountProperty, value);
    }

    public double MinLineWidth
    {
        get => GetValue(MinLineWidthProperty);
        set => SetValue(MinLineWidthProperty, value);
    }

    public double MaxLineWidth
    {
        get => GetValue(MaxLineWidthProperty);
        set => SetValue(MaxLineWidthProperty, value);
    }

    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public ObservableCollection<double> Lines => _lineWidths;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LineCountProperty ||
            change.Property == MinLineWidthProperty ||
            change.Property == MaxLineWidthProperty)
        {
            BuildLines();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        MotionPolicy.ReducedMotionChanged -= OnReducedMotionChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyMotionMode(MotionPolicy.IsReducedMotion);
    }

    private void OnReducedMotionChanged(object? sender, bool isReducedMotion) =>
        ApplyMotionMode(isReducedMotion);

    private void ApplyMotionMode(bool isReducedMotion)
    {
        foreach (var child in this.GetVisualDescendants().OfType<Border>())
        {
            if (!child.Classes.Contains("shimmer-line"))
                continue;

            if (isReducedMotion)
            {
                child.Classes.Remove("animated");
                child.Classes.Add("reduced");
                child.Opacity = 0.55;
            }
            else
            {
                child.Classes.Add("animated");
                child.Classes.Remove("reduced");
                child.Opacity = 1;
            }
        }
    }

    private void BuildLines()
    {
        _lineWidths.Clear();

        if (LineCount <= 0)
            return;

        for (var index = 0; index < LineCount; index++)
        {
            var factor = 0.78 + ((index % 3) * 0.09);
            var width = MinLineWidth + ((MaxLineWidth - MinLineWidth) * factor);
            _lineWidths.Add(Math.Min(MaxLineWidth, Math.Round(width, 1)));
        }

        if (Lines.Count > 0)
            Lines[0] = MaxLineWidth;
    }
}
