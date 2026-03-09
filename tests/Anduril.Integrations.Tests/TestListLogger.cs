using Microsoft.Extensions.Logging;

namespace Anduril.Integrations.Tests;

internal sealed class TestListLogger<T> : ILogger<T>
{
    public List<string> WarningMessages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel != LogLevel.Warning)
            return;

        WarningMessages.Add(formatter(state, exception));
    }
}