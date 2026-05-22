using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

public sealed class BreadcrumbSegment
{
    public required string Label { get; init; }

    public bool IsFirst { get; init; }
}

public partial class PathBreadcrumb : UserControl
{
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<PathBreadcrumb, string?>(nameof(Path));

    public PathBreadcrumb()
    {
        InitializeComponent();
    }

    public ObservableCollection<BreadcrumbSegment> Segments { get; } = [];

    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
            RebuildSegments(change.GetNewValue<string?>());
    }

    private void RebuildSegments(string? path)
    {
        Segments.Clear();

        if (string.IsNullOrWhiteSpace(path))
            return;

        var parts = path
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < parts.Length; index++)
        {
            Segments.Add(new BreadcrumbSegment
            {
                Label = parts[index],
                IsFirst = index == 0
            });
        }
    }
}
