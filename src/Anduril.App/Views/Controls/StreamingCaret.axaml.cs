using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

/// <summary>
/// Small 1×16 caret used while streaming output.
/// </summary>
public partial class StreamingCaret : UserControl
{
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<StreamingCaret, bool>(nameof(IsStreaming));

    public StreamingCaret()
    {
        InitializeComponent();
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

        if (change.Property == IsStreamingProperty && Caret is not null)
            Caret.IsVisible = (bool)change.NewValue!;
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
        if (Caret is null)
            return;

        Caret.Classes.Set("blink", !isReducedMotion);
        Caret.Opacity = isReducedMotion ? 1 : Caret.Opacity;
    }
}
