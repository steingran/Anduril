using System;
using Avalonia;
using Avalonia.Controls;

namespace Anduril.App.Views.Controls;

/// <summary>
/// Static helper + attached property exposing reduced-motion intent.
/// Animation styles consult <see cref="IsReducedMotion"/> to short-circuit
/// durations to zero. Full enforcement (binding to OS user-settings and
/// the in-app preference) lands with ANDA-76; this stub gives every
/// surface a single seam to flip.
/// </summary>
public static class MotionPolicy
{
    public static event EventHandler<bool>? ReducedMotionChanged;

    /// <summary>
    /// True when motion should be suppressed. Defaults to false; ANDA-76
    /// will wire this to <c>Application.Current.PlatformSettings</c> and
    /// <c>UserPreferences</c>.
    /// </summary>
    public static bool IsReducedMotion
    {
        get => _isReducedMotion;
        set
        {
            if (_isReducedMotion == value)
                return;

            _isReducedMotion = value;
            ReducedMotionChanged?.Invoke(null, value);
        }
    }

    private static bool _isReducedMotion;

    /// <summary>
    /// Returns <see cref="TimeSpan.Zero"/> when reduced-motion is in
    /// effect, otherwise <paramref name="duration"/>.
    /// </summary>
    public static TimeSpan ResolveDuration(TimeSpan duration)
        => IsReducedMotion ? TimeSpan.Zero : duration;

    /// <summary>
    /// Per-control reduced-motion override that XAML triggers can react to.
    /// Independent of the global <see cref="IsReducedMotion"/> flag — surfaces
    /// typically bind via DynamicResource on the duration tokens, but a Style
    /// may opt a specific subtree into reduced-motion via this attached
    /// property. ANDA-76 will introduce the binding that fans the global
    /// policy out to controls when needed.
    /// </summary>
    public static readonly AttachedProperty<bool> IsReducedMotionProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsReducedMotion", typeof(MotionPolicy), defaultValue: false);

    public static bool GetIsReducedMotion(Control control) =>
        control.GetValue(IsReducedMotionProperty);

    public static void SetIsReducedMotion(Control control, bool value) =>
        control.SetValue(IsReducedMotionProperty, value);
}
