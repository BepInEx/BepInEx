using System;
using BepInEx.Configuration;

namespace BepInEx.Logging;

public class FilteredConsoleLogListener : ILogListener
{
    protected static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
        "Logging.Console", "LogLevels",
        LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
        "Only displays the specified log levels in the console output.");

    public volatile string SourceFilter;

    public LogLevel LogLevelFilter => ConfigConsoleDisplayedLevel.Value;

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if (SourceFilter != null &&
            !string.Equals(eventArgs.Source.SourceName, SourceFilter, StringComparison.OrdinalIgnoreCase))
            return;

        InputConsole.WriteLogLine(eventArgs.ToStringLine(), eventArgs.Level.GetConsoleColor());
    }

    public void Dispose() { }
}
