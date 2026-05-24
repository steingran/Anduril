using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Anduril.App.Views.Controls;

/// <summary>
/// A three-dot animated indicator shown while the AI is streaming a response.
/// Bind <see cref="IsStreaming"/> to your view-model's streaming flag.
/// </summary>
public partial class StreamingDotIndicator : UserControl
{
    private readonly Ellipse[] _dots;

    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<StreamingDotIndicator, bool>(nameof(IsStreaming));

    public StreamingDotIndicator()
    {
        InitializeComponent();
        _dots = [Dot1, Dot2, Dot3];
        MotionPolicy.ReducedMotionChanged += OnReducedMotionChanged;
        ApplyMotionMode(MotionPolicy.IsReducedMotion);
    }

    public bool IsStreaming
    {
        get => GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsStreamingProperty && DotsPanel is not null)
            DotsPanel.IsVisible = (bool)change.NewValue!;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        MotionPolicy.ReducedMotionChanged -= OnReducedMotionChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnReducedMotionChanged(object? sender, bool isReducedMotion) =>
        ApplyMotionMode(isReducedMotion);

    private void ApplyMotionMode(bool isReducedMotion)
    {
        foreach (var dot in _dots)
            dot.Classes.Set("animate", !isReducedMotion);

        if (isReducedMotion)
        {
            Dot1.Opacity = 1.0;
            Dot2.Opacity = 0.55;
            Dot3.Opacity = 0.55;
            return;
        }

        foreach (var dot in _dots)
            dot.Opacity = 0.3;
    }
}
