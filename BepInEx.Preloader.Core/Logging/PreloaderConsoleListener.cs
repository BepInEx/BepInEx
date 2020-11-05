using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core.Logging
{
	public class PreloaderConsoleListener : ILogListener
	{
		public static List<LogEventArgs> LogEvents { get; } = new List<LogEventArgs>();

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if ((eventArgs.Level & ConfigConsoleDisplayedLevel.Value) == 0)
				return;

			LogEvents.Add(eventArgs);
		}

		private static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
			"Which log levels to show in the console output.");

		public void Dispose() { }
	}
}