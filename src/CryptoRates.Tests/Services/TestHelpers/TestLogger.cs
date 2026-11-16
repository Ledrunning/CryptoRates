using Microsoft.Extensions.Logging;

namespace CryptoRates.Tests.Services.TestHelpers;

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>A tiny in-memory logger: enough to assert on warnings without extra packages.</summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> entries = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (entries)
            {
                return [.. entries];
            }
        }
    }

    public IEnumerable<LogEntry> Warnings => Entries.Where(e => e.Level == LogLevel.Warning);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (entries)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
