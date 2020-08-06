using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.Preloader
{
	/// <summary>
	/// Log listener that listens to logs during preloading time and buffers messages for output in Unity logs later.
	/// </summary>
	public class PreloaderConsoleListener : ILogListener
	{
		/// <summary>
		/// 
		/// </summary>
		public static List<LogEventArgs> LogEvents { get; } = new List<LogEventArgs>();

		/// <inheritdoc />
		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if ((eventArgs.Level & ConfigConsoleDisplayedLevel.Value) == 0)
				return;
			
			LogEvents.Add(eventArgs);
		}
		
		private static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Console","LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info | LogLevel.Warning,
			"Which log levels to show in the console output.");

		/// <inheritdoc />
		public void Dispose() { }
	}
}