using System.Globalization;
using Anduril.App.Models;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace Anduril.App.Converters;

/// <summary>
/// Maps a <see cref="DiffLineKind"/> value to a background <see cref="IBrush"/>
/// for use in the diff line ItemsControl.
/// </summary>
public sealed class DiffLineBackgroundConverter : IValueConverter
{
    public static readonly DiffLineBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffLineKind kind ? kind switch
        {
            DiffLineKind.Added => GetBrush("AndurilDiffAddBrush"),
            DiffLineKind.Removed => GetBrush("AndurilDiffRemoveBrush"),
            _ => GetBrush("AndurilDiffContextBrush")
        } : GetBrush("AndurilDiffContextBrush");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }
}
