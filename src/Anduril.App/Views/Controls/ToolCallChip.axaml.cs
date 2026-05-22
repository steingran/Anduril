using Anduril.App.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Anduril.App.Views.Controls;

/// <summary>
/// Compact inline chip that shows a tool call and optional detail body.
/// </summary>
public partial class ToolCallChip : UserControl
{
    public static readonly StyledProperty<ToolCallSummary?> SummaryProperty =
        AvaloniaProperty.Register<ToolCallChip, ToolCallSummary?>(nameof(Summary));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCallChip, bool>(nameof(IsExpanded));

    public ToolCallChip()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public ToolCallSummary? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SummaryProperty || change.Property == IsExpandedProperty)
            UpdateVisualState();
    }

    private void OnToggleClicked(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void UpdateVisualState()
    {
        if (ToolDetail is null || NameText is null || Chevron is null)
            return;

        NameText.Text = Summary?.ToolName ?? "Unknown tool";
        ToolDetail.Text = Summary?.Detail;

        Chevron.Classes.Set("expanded", IsExpanded);

        ToolDetail.IsVisible = IsExpanded &&
                               !string.IsNullOrWhiteSpace(Summary?.Detail);
    }
}
