using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Anduril.App.Views.Controls;

public partial class SegmentedControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SegmentedControl, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SegmentedControl, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SegmentedControl, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool _isSyncingSelection;
    private readonly EventHandler<VisualTreeAttachmentEventArgs> _attachedToVisualTreeHandler;
    private readonly EventHandler _layoutUpdatedHandler;

    public SegmentedControl()
    {
        InitializeComponent();
        AutomationProperties.SetName(this, "Segmented control");
        AutomationProperties.SetHelpText(this, "Segmented control with keyboard left/right selection");
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Tab);

        _attachedToVisualTreeHandler = (_, _) => RepositionThumb();
        _layoutUpdatedHandler = (_, _) => RepositionThumb();

        MotionPolicy.ReducedMotionChanged += OnReducedMotionChanged;
        ApplyMotionMode(MotionPolicy.IsReducedMotion);

        Segments.AttachedToVisualTree += _attachedToVisualTreeHandler;
        Segments.LayoutUpdated += _layoutUpdatedHandler;
        Segments.KeyDown += OnListKeyDown;
        Segments.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        Segments.SelectionChanged += OnSelectionChanged;
        RepositionThumb();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int ItemCount
    {
        get
        {
            if (ItemsSource is null)
                return 0;

            if (ItemsSource is System.Collections.ICollection items)
                return items.Count;

            return ItemsSource.Cast<object>().Count();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedIndexProperty)
        {
            UpdateSelectionFromIndex();
            RepositionThumb();
            return;
        }

        if (change.Property == SelectedItemProperty)
        {
            UpdateSelectionFromItem();
            RepositionThumb();
            return;
        }

        if (change.Property == ItemsSourceProperty)
        {
            if (ItemCount == 0)
            {
                SelectedIndex = -1;
                SelectedItem = null;
                return;
            }

            if (SelectedIndex < 0)
                SelectedIndex = 0;
            else if (SelectedIndex >= ItemCount)
                SelectedIndex = ItemCount - 1;

            UpdateSelectionFromIndex();
            RepositionThumb();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        MotionPolicy.ReducedMotionChanged -= OnReducedMotionChanged;
        Segments.AttachedToVisualTree -= _attachedToVisualTreeHandler;
        Segments.LayoutUpdated -= _layoutUpdatedHandler;
        Segments.KeyDown -= OnListKeyDown;
        Segments.RemoveHandler(KeyDownEvent, OnListKeyDown);
        Segments.SelectionChanged -= OnSelectionChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ControlAutomationPeer(this);

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
            return;

        _isSyncingSelection = true;
        try
        {
            SelectedItem = Segments.SelectedItem;
            SelectedIndex = Segments.SelectedIndex;
        }
        finally
        {
            _isSyncingSelection = false;
            RepositionThumb();
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (ItemCount == 0)
            return;

        var next = SelectedIndex;
        if (e.Key == Key.Left)
            next = (SelectedIndex - 1 + ItemCount) % ItemCount;
        else if (e.Key == Key.Right)
            next = (SelectedIndex + 1) % ItemCount;

        if (next == SelectedIndex)
            return;

        SelectedIndex = next;
        Segments.SelectedIndex = next;
        e.Handled = true;
        RepositionThumb();
    }

    private void OnReducedMotionChanged(object? sender, bool isReducedMotion) =>
        ApplyMotionMode(isReducedMotion);

    private void ApplyMotionMode(bool isReducedMotion)
    {
        if (SelectionThumb is null)
            return;

        SelectionThumb.Classes.Set("animate", !isReducedMotion);
    }

    private void UpdateSelectionFromIndex()
    {
        if (_isSyncingSelection || Segments is null || ItemCount == 0)
            return;

        _isSyncingSelection = true;
        try
        {
            if (SelectedIndex < 0 || SelectedIndex >= ItemCount)
            {
                Segments.SelectedItem = null;
                Segments.SelectedIndex = -1;
                SelectedItem = null;
                return;
            }

            var items = ItemsSource?.Cast<object>().ToList() ?? [];
            var target = items.ElementAtOrDefault(SelectedIndex);
            Segments.SelectedIndex = SelectedIndex;
            SelectedItem = target;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void UpdateSelectionFromItem()
    {
        if (_isSyncingSelection || ItemsSource is null)
            return;

        if (SelectedItem is null)
        {
            Segments.SelectedIndex = -1;
            Segments.SelectedItem = null;
            SelectedIndex = -1;
            return;
        }

        var items = ItemsSource.Cast<object>().ToList();
        var index = items.IndexOf(SelectedItem);
        if (index < 0)
        {
            Segments.SelectedIndex = -1;
            Segments.SelectedItem = null;
            return;
        }

        _isSyncingSelection = true;
        try
        {
            Segments.SelectedIndex = index;
            SelectedIndex = index;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void RepositionThumb()
    {
        if (SelectionThumb is null || Segments is null || Segments.SelectedItem is null)
        {
            if (SelectionThumb is not null)
                SelectionThumb.IsVisible = false;
            return;
        }

        var selectedContainer = Segments.ContainerFromItem(Segments.SelectedItem);
        if (selectedContainer is null)
            return;

        var segmentBounds = selectedContainer.Bounds;
        SelectionThumb.IsVisible = true;
        SelectionThumb.Margin = new Thickness(segmentBounds.X + 2, 0, 0, 0);
        SelectionThumb.Width = Math.Max(segmentBounds.Width, 1);
    }
}
