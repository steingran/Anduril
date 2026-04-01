using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

/// <summary>
/// A three-dot animated indicator shown while the AI is streaming a response.
/// Bind <see cref="IsStreaming"/> to your view-model's streaming flag.
/// </summary>
public partial class StreamingDotIndicator : UserControl
{
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<StreamingDotIndicator, bool>(nameof(IsStreaming));

    public StreamingDotIndicator()
    {
        InitializeComponent();
    }

    public bool IsStreaming
    {
        get => GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsStreamingProperty)
            DotsPanel.IsVisible = (bool)change.NewValue!;
    }
}

