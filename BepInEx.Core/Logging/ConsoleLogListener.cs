using System;
using BepInEx.Configuration;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using a console spawned by BepInEx.
	/// </summary>
	public class ConsoleLogListener : ILogListener
	{
		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (eventArgs.Level.GetHighestLevel() > ConfigConsoleDisplayedLevel.Value)
				return;

			string log = $"[{eventArgs.Level,-7}:{((ILogSource)sender).SourceName,10}] {eventArgs.Data}\r\n";

			ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
			Console.Write(log);
			ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
		}

		public void Dispose() { }

		private static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console","DisplayedLogLevel",
			LogLevel.Info,
			"Only displays the specified log level and above in the console output.");
	}
}