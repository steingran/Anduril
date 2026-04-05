using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Anduril.App.Converters;

/// <summary>
/// Maps a boolean availability flag to a status-dot brush:
/// true → green (#4CAF73), false → dim grey (#3A4460).
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    private static readonly SolidColorBrush AvailableBrush   = new(Color.FromRgb(0x4C, 0xAF, 0x73));
    private static readonly SolidColorBrush UnavailableBrush = new(Color.FromRgb(0x3A, 0x44, 0x60));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? AvailableBrush : UnavailableBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
