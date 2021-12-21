using System;

namespace BepInEx.Logging;

/// <summary>
///     A generic log listener that receives log events and can route them to some output (e.g. file, console, socket).
/// </summary>
public interface ILogListener : IDisposable
{
    /// <summary>
    ///     What log levels the listener preliminarily wants.
    /// </summary>
    /// <remarks>
    ///     The filter is used to more efficiently discard log messages that aren't being listened to.
    ///     As such, the filter should represent the log levels that the listener will always want to process.
    ///     It is up to the the implementation of <see cref="LogEvent" /> whether the messages are going to be processed or
    ///     discarded.
    /// </remarks>
    /// TODO: Right now the filter cannot be updated after the log listener has been attached to the logger.
    LogLevel LogLevelFilter { get; }

    /// <summary>
    ///     Handle an incoming log event.
    /// </summary>
    /// <param name="sender">Log source that sent the event. Don't use; instead use <see cref="LogEventArgs.Source" /></param>
    /// <param name="eventArgs">Information about the log message.</param>
    void LogEvent(object sender, LogEventArgs eventArgs);
}
