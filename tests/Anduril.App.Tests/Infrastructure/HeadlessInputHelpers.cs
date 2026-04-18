using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Anduril.App.Tests.Infrastructure;

/// <summary>
/// Thin, test-only wrappers around the Avalonia.Headless input extensions. They keep test
/// bodies readable and route every keystroke and pointer event through the real input
/// pipeline (focus routing, hit-testing, gesture handling), so we cover the same paths a
/// real user exercises rather than short-circuiting to a command or a bound property.
/// </summary>
internal static class HeadlessInputHelpers
{
    /// <summary>Injects a text input event into the window, as if the user typed the given text.</summary>
    public static void TypeText(this Window window, string text) =>
        window.KeyTextInput(text);

    /// <summary>
    /// Presses and releases a physical key with optional modifiers (press + release in one call).
    /// Uses the hardware-level <c>KeyPressQwerty</c> overload so the headless platform translates
    /// the physical key into the current keyboard layout's <see cref="Key"/> value — that is the
    /// same path real users take, and it avoids the obsolete <c>KeyPress(Key, ...)</c> overload.
    /// </summary>
    public static void PressKey(this Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None) =>
        window.KeyPressQwerty(key, modifiers);

    /// <summary>
    /// Performs a single mouse click (move + down + up) at the given window-space point.
    /// No modifiers, no prior-button release, and no pause between down/up — sufficient for the
    /// single-click interactions these tests need. If a future test needs double-click,
    /// modifiers, or a drag, extend this with overloads rather than papering over it here.
    /// </summary>
    public static void Click(this Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseMove(point);
        window.MouseDown(point, button);
        window.MouseUp(point, button);
    }

    /// <summary>
    /// Clicks the visual center of <paramref name="control"/> using real pointer events routed
    /// through the window. Throws if the control has not been laid out (zero bounds) — tests
    /// must call <see cref="Avalonia.Threading.Dispatcher.UIThread.RunJobs()"/> after
    /// <see cref="Window.Show"/> before asking for bounds.
    /// </summary>
    public static void ClickCenterOf(this Window window, Control control, MouseButton button = MouseButton.Left)
    {
        var center = window.GetCenterOf(control);
        window.Click(center, button);
    }

    /// <summary>
    /// Translates the center of <paramref name="control"/> into the window's client coordinate
    /// space. Throws if the control has zero bounds so that a missing layout pass fails loudly
    /// rather than silently clicking (0,0).
    /// </summary>
    public static Point GetCenterOf(this Window window, Control control)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot click control of type {control.GetType().Name}: bounds are {control.Bounds}. " +
                "Did you forget to call Dispatcher.UIThread.RunJobs() after window.Show() to force a layout pass?");
        }

        var localCenter = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var windowPoint = control.TranslatePoint(localCenter, window)
            ?? throw new InvalidOperationException(
                $"Control of type {control.GetType().Name} is not connected to window {window.GetType().Name}.");
        return windowPoint;
    }

    /// <summary>
    /// Walks the visual tree under <paramref name="root"/> and returns the first descendant of
    /// type <typeparamref name="T"/> that matches <paramref name="predicate"/>. Throws
    /// <see cref="InvalidOperationException"/> if nothing matches — callers should treat a
    /// missing control as a test failure, not as a soft miss.
    /// </summary>
    public static T FindDescendant<T>(this Visual root, Func<T, bool> predicate) where T : Visual =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(predicate)
            ?? throw new InvalidOperationException(
                $"No descendant of type {typeof(T).Name} matched the predicate under {root.GetType().Name}.");

    /// <summary>
    /// Pumps the Avalonia UI dispatcher up to <paramref name="maxIterations"/> times, yielding
    /// between each iteration so Task-returning continuations (e.g. a <c>ReactiveCommand</c>'s
    /// async body, or a fire-and-forget init method on a view model) get scheduler time to run.
    /// Returns as soon as <paramref name="condition"/> becomes <c>true</c>; throws
    /// <see cref="TimeoutException"/> with the attempted iteration count if it never does so the
    /// failure mode is loud rather than a flaky hang.
    /// </summary>
    public static async Task FlushUntilAsync(Func<bool> condition, int maxIterations, string timeoutMessage)
    {
        var i = 0;
        for (; i < maxIterations && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        if (!condition())
        {
            throw new TimeoutException($"{timeoutMessage} after {i} dispatcher flushes.");
        }
    }
}
