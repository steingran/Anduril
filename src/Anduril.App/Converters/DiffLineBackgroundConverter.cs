using System.Globalization;
using Anduril.App.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Anduril.App.Converters;

/// <summary>
/// Maps a <see cref="DiffLineKind"/> value to a background <see cref="IBrush"/>
/// for use in the diff line ItemsControl.
/// </summary>
public sealed class DiffLineBackgroundConverter : IValueConverter
{
    public static readonly DiffLineBackgroundConverter Instance = new();

    // Subtle dark-mode palette: green tint for added, red tint for removed, transparent for context
    private static readonly SolidColorBrush AddedBrush   = new(Color.FromRgb(0x0E, 0x2A, 0x14));
    private static readonly SolidColorBrush RemovedBrush = new(Color.FromRgb(0x2A, 0x0E, 0x0E));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffLineKind kind ? kind switch
        {
            DiffLineKind.Added   => AddedBrush,
            DiffLineKind.Removed => RemovedBrush,
            _                    => Brushes.Transparent
        } : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

