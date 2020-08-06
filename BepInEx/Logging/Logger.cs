using System;
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
		public static ICollection<ILogListener> Listeners { get; } = new List<ILogListener>();

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
			foreach (var listener in Listeners)
			{
				listener?.LogEvent(sender, eventArgs);
			}
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


		private class LogSourceCollection : List<ILogSource>, ICollection<ILogSource>
		{
			void ICollection<ILogSource>.Add(ILogSource item)
			{
				if (item == null)
					throw new ArgumentNullException(nameof(item), "Log sources cannot be null when added to the source list.");

				item.LogEvent += InternalLogEvent;

				base.Add(item);
			}

			void ICollection<ILogSource>.Clear()
			{
				foreach (var item in base.ToArray())
				{
					((ICollection<ILogSource>)this).Remove(item);
				}
			}

			bool ICollection<ILogSource>.Remove(ILogSource item)
			{
				if (item == null)
					return false;

				if (!base.Contains(item))
					return false;

				item.LogEvent -= InternalLogEvent;

				base.Remove(item);

				return true;
			}
		}
	}
}