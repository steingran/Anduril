using System.Globalization;
using Anduril.App.Models;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Anduril.App.Converters;

/// <summary>
/// Maps a <see cref="DiffLineKind"/> value to a foreground <see cref="IBrush"/> for
/// diff preview lines.
/// </summary>
public sealed class DiffLineForegroundBrushConverter : IValueConverter
{
    public static readonly DiffLineForegroundBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffLineKind kind ? kind switch
        {
            DiffLineKind.Added => GetBrush("AndurilDiffAddTextBrush"),
            DiffLineKind.Removed => GetBrush("AndurilDiffRemoveTextBrush"),
            _ => GetBrush("AndurilTextPrimaryBrush")
        } : GetBrush("AndurilTextPrimaryBrush");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return Brushes.White;
    }
}
