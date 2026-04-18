using Avalonia.Headless;

namespace Anduril.App.Tests.Infrastructure;

/// <summary>
/// Base class for Avalonia headless E2E tests using TUnit.
///
/// Avalonia.Headless ships pre-built integrations for XUnit and NUnit but not TUnit, so this
/// class provides the equivalent plumbing: it starts a single <see cref="HeadlessUnitTestSession"/>
/// lazily on first use and exposes <see cref="RunOnUIThread"/> to dispatch test bodies onto the
/// Avalonia UI thread that the session runs.
///
/// <para>
/// <c>AppBuilder.Configure</c> can only be called once per process, so the session is held in a
/// static <see cref="Lazy{T}"/> and shared across every test class that inherits from this base.
/// </para>
/// </summary>
public abstract class AvaloniaHeadlessTestBase
{
    private static readonly Lazy<HeadlessUnitTestSession> _session = new(
        () => HeadlessUnitTestSession.StartNew(typeof(TestApp)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    protected static HeadlessUnitTestSession Session => _session.Value;

    /// <summary>
    /// Runs <paramref name="action"/> on the Avalonia UI thread owned by the headless session
    /// and awaits its completion. Exceptions thrown inside <paramref name="action"/> propagate
    /// back to the caller.
    /// </summary>
    protected static Task RunOnUIThread(Func<Task> action) =>
        _session.Value.Dispatch(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, CancellationToken.None);

    /// <summary>
    /// Runs <paramref name="action"/> on the Avalonia UI thread and returns its result.
    /// </summary>
    protected static Task<T> RunOnUIThread<T>(Func<Task<T>> action) =>
        _session.Value.Dispatch(action, CancellationToken.None);
}
