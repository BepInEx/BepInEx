using System;
using System.Collections.Generic;

namespace BepInEx.Logging
{
    /// <summary>
    ///     Handles pub-sub event marshalling across all log listeners and sources.
    /// </summary>
    public static class Logger
    {
        private static readonly ManualLogSource InternalLogSource;

        /// <summary>
        ///     Collection of all log listeners that receive log events.
        /// </summary>
        public static ICollection<ILogListener> Listeners { get; }

        /// <summary>
        ///     Collection of all log source that output log events.
        /// </summary>
        public static ICollection<ILogSource> Sources { get; }

        static Logger()
        {
            Sources = new LogSourceCollection();
            Listeners = new List<ILogListener>();

            InternalLogSource = CreateLogSource("BepInEx");
        }

        internal static void InternalLogEvent(object sender, LogEventArgs eventArgs)
        {
            foreach (var listener in Listeners) listener?.LogEvent(sender, eventArgs);
        }

        /// <summary>
        ///     Logs an entry to the internal logger instance.
        /// </summary>
        /// <param name="level">The level of the entry.</param>
        /// <param name="data">The data of the entry.</param>
        internal static void Log(LogLevel level, object data) => InternalLogSource.Log(level, data);

        internal static void LogFatal(object data) => Log(LogLevel.Fatal, data);
        internal static void LogError(object data) => Log(LogLevel.Error, data);
        internal static void LogWarning(object data) => Log(LogLevel.Warning, data);
        internal static void LogMessage(object data) => Log(LogLevel.Message, data);
        internal static void LogInfo(object data) => Log(LogLevel.Info, data);
        internal static void LogDebug(object data) => Log(LogLevel.Debug, data);

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
}
