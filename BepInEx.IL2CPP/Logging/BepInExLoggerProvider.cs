using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Microsoft.Extensions.Logging;
using BepInExLogLevel = BepInEx.Logging.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace BepInEx.IL2CPP.Logging;

internal class BepInExLoggerProvider : ILoggerProvider
{
    private readonly List<BepInExLogger> loggers = new();

    public void Dispose()
    {
        foreach (var bepInExLogger in loggers)
            bepInExLogger.Dispose();
        loggers.Clear();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new BepInExLogger { SourceName = categoryName };
        Logger.Sources.Add(logger);
        loggers.Add(logger);
        return logger;
    }

    private class EmptyScope : IDisposable
    {
        public void Dispose() { }
    }

    private class BepInExLogger : ILogSource, ILogger
    {
        public void Log<TState>(LogLevel logLevel,
                                EventId eventId,
                                TState state,
                                Exception exception,
                                Func<TState, Exception, string> formatter)
        {
            var logLine = state.ToString() ?? string.Empty;

            if (exception != null)
                logLine += $"\nException: {exception}";

            LogEvent?.Invoke(this,
                             new LogEventArgs(logLine, MSLogLevelToBepInExLogLevel(logLevel),
                                              this));
        }


        public bool IsEnabled(LogLevel logLevel) =>
            (MSLogLevelToBepInExLogLevel(logLevel) & Logger.ListenedLogLevels) != BepInExLogLevel.None;

        public IDisposable BeginScope<TState>(TState state) => new EmptyScope();

        public void Dispose() => Logger.Sources.Remove(this);

        public string SourceName { get; init; }

        public event EventHandler<LogEventArgs> LogEvent;

        private static BepInExLogLevel MSLogLevelToBepInExLogLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => BepInExLogLevel.Debug,
            LogLevel.Debug       => BepInExLogLevel.Debug,
            LogLevel.Information => BepInExLogLevel.Info,
            LogLevel.Warning     => BepInExLogLevel.Warning,
            LogLevel.Error       => BepInExLogLevel.Error,
            LogLevel.Critical    => BepInExLogLevel.Fatal,
            LogLevel.None        => BepInExLogLevel.None,
            var _                => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
    }
}
