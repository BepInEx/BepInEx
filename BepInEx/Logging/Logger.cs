using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace BepInEx
{
	/// <summary>
	/// A static <see cref="BaseLogger"/> instance.
	/// </summary>
	public static class Logger
	{
		public static ICollection<ILogListener> Listeners { get; } = new List<ILogListener>();

		public static ICollection<ILogSource> Sources { get; } = new LogSourceCollection();

		private static readonly ManualLogSource InternalLogSource = CreateLogSource("BepInEx");

		private static void InternalLogEvent(object sender, LogEventArgs eventArgs)
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
		/// <param name="entry">The textual value of the entry.</param>
		public static void Log(LogLevel level, object data)
		{
			InternalLogSource.Log(level, data);
		}

		public static void LogFatal(object data) => Log(LogLevel.Fatal, data);
		public static void LogError(object data) => Log(LogLevel.Error, data);
		public static void LogWarning(object data) => Log(LogLevel.Warning, data);
		public static void LogMessage(object data) => Log(LogLevel.Message, data);
		public static void LogInfo(object data) => Log(LogLevel.Info, data);
		public static void LogDebug(object data) => Log(LogLevel.Debug, data);

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