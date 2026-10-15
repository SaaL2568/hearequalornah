using System.IO;
using Microsoft.Extensions.Logging;

namespace HearEqualOrNah.UI;

/// <summary>
/// Minimal file logger: writes to %LOCALAPPDATA%\hearequalornah\app.log (recreated each launch) so
/// measurement attempts can be diagnosed after the fact.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    public FileLoggerProvider(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private void Write(string line)
    {
        lock (_sync)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _category = categoryName[(categoryName.LastIndexOf('.') + 1)..];
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = $"{DateTime.Now:HH:mm:ss.fff} {logLevel,-11} {_category}: {formatter(state, exception)}";
            if (exception != null)
                line += Environment.NewLine + exception;
            _provider.Write(line);
        }
    }
}
