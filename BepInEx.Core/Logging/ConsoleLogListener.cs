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
			if ((eventArgs.Level & ConfigConsoleDisplayedLevel.Value) == 0)
				return;

			ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
			Console.Write(eventArgs.ToStringLine());
			ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
		}

		public void Dispose() { }

		public static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info,
			"Only displays the specified log levels in the console output.");
	}
}