using System;
using BepInEx.Configuration;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class ConsoleLogListener : ILogListener
	{
		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if ((eventArgs.Level & ConfigConsoleDisplayedLevel.Value) == 0)
				return;
			
			ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
			Log(eventArgs);
			ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
		}

		private void Log(LogEventArgs eventArgs)
		{
			// Special case: if message comes from Unity log, it's already logged by Unity, in which case 
			// we replay it only in the visible console
			if (eventArgs.Source is UnityLogSource)
				ConsoleManager.ConsoleStream?.Write(eventArgs.ToStringLine());
			else
				Console.Write(eventArgs.ToStringLine());
		}

		public void Dispose() { }

		private static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console","LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info | LogLevel.Warning,
			"Which log levels to show in the console output.");
	}
}