using System;
using BepInEx.Configuration;
using BepInEx.ConsoleUtil;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class ConsoleLogListener : ILogListener
	{
		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if ((eventArgs.Level & ConfigConsoleDisplayedLevel.Value) > 0)
				return;

			string log = $"[{eventArgs.Level,-7}:{((ILogSource)sender).SourceName,10}] {eventArgs.Data}\r\n";

			Kon.ForegroundColor = eventArgs.Level.GetConsoleColor();
			Console.Write(log);
			Kon.ForegroundColor = ConsoleColor.Gray;
		}

		public void Dispose() { }

		private static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console","DisplayedLogLevel",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info,
			"Only displays the specified log levels in the console output.");
	}
}