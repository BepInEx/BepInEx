using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;

namespace BepInEx.Logging;

/// <summary>
///     Handles pub-sub event marshalling across all log listeners and sources.
/// </summary>
public static class Logger
{
    private static readonly ManualLogSource InternalLogSource;

    private static readonly LogListenerCollection listeners;

    static Logger()
    {
        Sources = new LogSourceCollection();
        listeners = new LogListenerCollection();

        InternalLogSource = CreateLogSource("BepInEx");
    }

    /// <summary>
    ///     Log levels that are currently listened to by at least one listener.
    /// </summary>
    public static LogLevel ListenedLogLevels => listeners.activeLogLevels;

    /// <summary>
    ///     Collection of all log listeners that receive log events.
    /// </summary>
    public static ICollection<ILogListener> Listeners => listeners;

    /// <summary>
    ///     Collection of all log source that output log events.
    /// </summary>
    public static ICollection<ILogSource> Sources { get; }

    internal static void InternalLogEvent(object sender, LogEventArgs eventArgs)
    {
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator Prevent extra allocations
        foreach (var listener in listeners)
            if ((eventArgs.Level & listener.LogLevelFilter) != LogLevel.None)
                listener?.LogEvent(sender, eventArgs);
    }

    /// <summary>
    ///     Logs an entry to the internal logger instance.
    /// </summary>
    /// <param name="level">The level of the entry.</param>
    /// <param name="data">The data of the entry.</param>
    internal static void Log(LogLevel level, object data) => InternalLogSource.Log(level, data);

    /// <summary>
    ///     Logs an entry to the internal logger instance if any log listener wants the message.
    /// </summary>
    /// <param name="level">The level of the entry.</param>
    /// <param name="logHandler">Log handler to resolve log from.</param>
    internal static void Log(LogLevel level,
                             [InterpolatedStringHandlerArgument("level")]
                             BepInExLogInterpolatedStringHandler logHandler) =>
        InternalLogSource.Log(level, logHandler);

    /// <summary>
    ///     Creates a new log source with a name and attaches it to <see cref="Sources" />.
    /// </summary>
    /// <param name="sourceName">Name of the log source to create.</param>
    /// <returns>An instance of <see cref="ManualLogSource" /> that allows to write logs.</returns>
    public static ManualLogSource CreateLogSource(string sourceName)
    {
        var source = new ManualLogSource(sourceName);
        Sources.Add(source);
        return source;
    }

    private class LogListenerCollection : List<ILogListener>, ICollection<ILogListener>
    {
        public LogLevel activeLogLevels = LogLevel.None;

        void ICollection<ILogListener>.Add(ILogListener item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            activeLogLevels |= item.LogLevelFilter;

            base.Add(item);
        }

        void ICollection<ILogListener>.Clear()
        {
            activeLogLevels = LogLevel.None;
            base.Clear();
        }

        bool ICollection<ILogListener>.Remove(ILogListener item)
        {
            if (item == null || !base.Remove(item))
                return false;

            activeLogLevels = LogLevel.None;

            foreach (var i in this)
                activeLogLevels |= i.LogLevelFilter;

            return true;
        }
    }


    private class LogSourceCollection : List<ILogSource>, ICollection<ILogSource>
    {
        void ICollection<ILogSource>.Add(ILogSource item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item),
                                                "Log sources cannot be null when added to the source list.");

            item.LogEvent += InternalLogEvent;

            base.Add(item);
        }

        void ICollection<ILogSource>.Clear()
        {
            foreach (var item in this)
                item.LogEvent -= InternalLogEvent;

            base.Clear();
        }

        bool ICollection<ILogSource>.Remove(ILogSource item)
        {
            if (item == null || !Remove(item))
                return false;

            item.LogEvent -= InternalLogEvent;
            return true;
        }
    }
}
