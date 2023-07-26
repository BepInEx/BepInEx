using System;
using System.Collections;
using System.Collections.Generic;

namespace BepInEx.Logging
{
	/// <summary>
	/// A static Logger instance.
	/// </summary>
	public static class Logger
	{
		/// <summary>
		/// Collection of all log listeners that receive log events.
		/// </summary>
		public static ICollection<ILogListener> Listeners => _Listeners;

		private static readonly LogListenerCollection _Listeners = new LogListenerCollection();

		/// <summary>
		/// Collection of all log source that output log events.
		/// </summary>
		public static ICollection<ILogSource> Sources { get; } = new LogSourceCollection();

		private static readonly ManualLogSource InternalLogSource = CreateLogSource("BepInEx");

		private static bool internalLogsInitialized;

		internal static void InitializeInternalLoggers()
		{
			if (internalLogsInitialized)
				return;

			Sources.Add(new HarmonyLogSource());

			internalLogsInitialized = true;
		}

		internal static void InternalLogEvent(object sender, LogEventArgs eventArgs)
		{
			_Listeners.SendLogEvent(sender, eventArgs);
		}

		/// <summary>
		/// Logs an entry to the current logger instance.
		/// </summary>
		/// <param name="level">The level of the entry.</param>
		/// <param name="data">The textual value of the entry.</param>
		internal static void Log(LogLevel level, object data)
		{
			InternalLogSource.Log(level, data);
		}

		internal static void LogFatal(object data) => Log(LogLevel.Fatal, data);
		internal static void LogError(object data) => Log(LogLevel.Error, data);
		internal static void LogWarning(object data) => Log(LogLevel.Warning, data);
		internal static void LogMessage(object data) => Log(LogLevel.Message, data);
		internal static void LogInfo(object data) => Log(LogLevel.Info, data);
		internal static void LogDebug(object data) => Log(LogLevel.Debug, data);

		/// <summary>
		/// Creates a new log source with a name and attaches it to log sources.
		/// </summary>
		/// <param name="sourceName">Name of the log source to create.</param>
		/// <returns>An instance of <see cref="ManualLogSource"/> that allows to write logs.</returns>
		public static ManualLogSource CreateLogSource(string sourceName)
		{
			var source = new ManualLogSource(sourceName);

			Sources.Add(source);

			return source;
		}

		private sealed class LogSourceCollection : ThreadSafeCollection<ILogSource>, ICollection<ILogSource>
		{
			public override void Add(ILogSource item)
			{
				if (item == null)
					throw new ArgumentNullException(nameof(item), "Log sources cannot be null when added to the source list.");

				lock (Lock)
				{
					item.LogEvent += InternalLogEvent;

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

				lock (Lock)
				{
					for (var i = 0; i < BaseList.Count; i++)
						BaseList[i].LogEvent -= InternalLogEvent;

					BaseList = new List<ILogSource>(0);
				}
			}

			public override bool Remove(ILogSource item)
			{
				if (item == null)
					return false;

				lock (Lock)
				{
					var wasPresent = base.Remove(item);
					if (wasPresent)
						item.LogEvent -= InternalLogEvent;
					return wasPresent;
				}
			}
		}

		private sealed class LogListenerCollection : ThreadSafeCollection<ILogListener>
		{
			public void SendLogEvent(object sender, LogEventArgs eventArgs)
			{
				// Do this instead of foreach to avoid boxing, also very slightly faster
				var aListInTime = BaseList;
				for (int i = 0; i < aListInTime.Count; i++)
					aListInTime[i].LogEvent(sender, eventArgs);
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
			protected readonly object Lock = new object();
			protected List<T> BaseList = new List<T>(0);

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

				lock (Lock)
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

				lock (Lock)
					BaseList = new List<T>(0);
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

				lock (Lock)
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

			public int Count => BaseList.Count;

			public bool IsReadOnly => false;
		}
	}
}