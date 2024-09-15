using System;
using System.Collections;
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
    public static LogLevel ListenedLogLevels => listeners.ActiveLogLevels;

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
        listeners.SendLogEvent(sender, eventArgs);
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

    private class LogListenerCollection : ThreadSafeCollection<ILogListener>
    {
        internal LogLevel ActiveLogLevels = LogLevel.None;

        internal void SendLogEvent(object sender, LogEventArgs eventArgs)
        {
            // Do this instead of foreach to avoid boxing, also very slightly faster
            var aListInTime = BaseList;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < aListInTime.Count; i++)
            {
                if ((eventArgs.Level & aListInTime[i].LogLevelFilter) != LogLevel.None)
                    aListInTime[i].LogEvent(sender, eventArgs);
            }
        }

        public override void Add(ILogListener item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (SpinLock)
            {
                ActiveLogLevels |= item.LogLevelFilter;
                base.Add(item);
            }
        }

        public override void Clear()
        {
            lock (SpinLock)
            {
                ActiveLogLevels = LogLevel.None;
                base.Clear();
            }
        }

        public override bool Remove(ILogListener item)
        {
            if (item == null || !base.Remove(item))
                return false;

            lock (SpinLock)
            {
                ActiveLogLevels = LogLevel.None;

                foreach (var i in this)
                    ActiveLogLevels |= i.LogLevelFilter;

                return true;
            }
        }
    }


    private class LogSourceCollection : ThreadSafeCollection<ILogSource>
    {
        public override void Add(ILogSource item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item),
                                                "Log sources cannot be null when added to the source list.");

            lock (SpinLock)
            {
                item.LogEvent += InternalLogEvent;
                base.Add(item);
                var copy = new List<ILogSource>(BaseList.Count + 1);
                copy.AddRange(BaseList);
                copy.Add(item);
                BaseList = copy;
            }
        }

        public override void Clear()
        {
            if (Count == 0)
                return;

            lock (SpinLock)
            {
                for (var i = 0; i < BaseList.Count; i++)
                    BaseList[i].LogEvent -= InternalLogEvent;

                BaseList = [];
            }
        }

        public override bool Remove(ILogSource item)
        {
            if (item == null)
                return false;

            lock (SpinLock)
            {
                var wasPresent = base.Remove(item);
                if (wasPresent)
                    item.LogEvent -= InternalLogEvent;
                return wasPresent;
            }
        }
    }
    
    /// <summary>
    /// Simple thread safe list that prioritizes read speed over write speed.
    /// Read is the same as a normal list, while write locks and allocates a copy of the list.
    /// Logger lists are rarely updated so this tradeoff should be fine.
    /// </summary>
    /// <inheritdoc />
    private class ThreadSafeCollection<T> : ICollection<T> where T : class
    {
        protected readonly object SpinLock = new();
        protected List<T> BaseList = [];

        public int Count => BaseList.Count;

        public bool IsReadOnly => false;

        public IEnumerator<T> GetEnumerator()
        {
            // Can't avoid boxing
            return BaseList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            // Can't avoid boxing
            return BaseList.GetEnumerator();
        }

        public virtual void Add(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "item can't be null");

            lock (SpinLock)
            {
                var copy = new List<T>(BaseList.Count + 1);
                copy.AddRange(BaseList);
                copy.Add(item);
                BaseList = copy;
            }
        }

        public virtual void Clear()
        {
            if (Count == 0)
                return;

            lock (SpinLock)
                BaseList = [];
        }

        public bool Contains(T item)
        {
            return BaseList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            BaseList.CopyTo(array, arrayIndex);
        }

        public virtual bool Remove(T item)
        {
            if (item == null)
                return false;

            lock (SpinLock)
            {
                var copy = new List<T>(BaseList.Count);
                var any = false;
                for (int i = 0; i < BaseList.Count; i++)
                {
                    var existingItem = BaseList[i];
                    if (existingItem.Equals(item))
                        any = true;
                    else
                        copy.Add(existingItem);
                }

                BaseList = copy;
                return any;
            }
        }
    }
}
