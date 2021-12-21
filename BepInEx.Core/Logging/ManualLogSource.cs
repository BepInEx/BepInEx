using System;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;

namespace BepInEx.Logging;

/// <summary>
///     A generic, multi-purpose log source. Exposes simple API to manually emit logs.
/// </summary>
public class ManualLogSource : ILogSource
{
    /// <summary>
    ///     Creates a manual log source.
    /// </summary>
    /// <param name="sourceName">Name of the log source.</param>
    public ManualLogSource(string sourceName)
    {
        SourceName = sourceName;
    }

    /// <inheritdoc />
    public string SourceName { get; }

    /// <inheritdoc />
    public event EventHandler<LogEventArgs> LogEvent;

    /// <inheritdoc />
    public void Dispose() { }

    /// <summary>
    ///     Logs a message with the specified log level.
    /// </summary>
    /// <param name="level">Log levels to attach to the message. Multiple can be used with bitwise ORing.</param>
    /// <param name="data">Data to log.</param>
    public void Log(LogLevel level, object data) => LogEvent?.Invoke(this, new LogEventArgs(data, level, this));

    /// <summary>
    ///     Logs an interpolated string with the specified log level.
    /// </summary>
    /// <param name="level">Log levels to attach to the message. Multiple can be used with bitwise ORing.</param>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void Log(LogLevel level,
                    [InterpolatedStringHandlerArgument("level")]
                    BepInExLogInterpolatedStringHandler logHandler)
    {
        if (logHandler.Enabled)
            LogEvent?.Invoke(this, new LogEventArgs(logHandler.ToString(), level, this));
    }

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Fatal" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogFatal(object data) => Log(LogLevel.Fatal, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Fatal" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogFatal(BepInExFatalLogInterpolatedStringHandler logHandler) => Log(LogLevel.Fatal, logHandler);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Error" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogError(object data) => Log(LogLevel.Error, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Error" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogError(BepInExErrorLogInterpolatedStringHandler logHandler) => Log(LogLevel.Error, logHandler);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Warning" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogWarning(object data) => Log(LogLevel.Warning, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Warning" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogWarning(BepInExWarningLogInterpolatedStringHandler logHandler) => Log(LogLevel.Warning, logHandler);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Message" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogMessage(object data) => Log(LogLevel.Message, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Message" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogMessage(BepInExMessageLogInterpolatedStringHandler logHandler) => Log(LogLevel.Message, logHandler);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Info" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogInfo(object data) => Log(LogLevel.Info, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Info" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogInfo(BepInExInfoLogInterpolatedStringHandler logHandler) => Log(LogLevel.Info, logHandler);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Debug" /> level.
    /// </summary>
    /// <param name="data">Data to log.</param>
    public void LogDebug(object data) => Log(LogLevel.Debug, data);

    /// <summary>
    ///     Logs an interpolated string with <see cref="LogLevel.Debug" /> level.
    /// </summary>
    /// <param name="logHandler">Handler for the interpolated string.</param>
    public void LogDebug(BepInExDebugLogInterpolatedStringHandler logHandler) => Log(LogLevel.Debug, logHandler);
}
