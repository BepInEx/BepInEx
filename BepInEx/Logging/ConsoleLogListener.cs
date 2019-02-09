using System;
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
			string log = $"[{eventArgs.Level}:{((ILogSource)sender).SourceName}] {eventArgs.Data}\r\n";

			Kon.ForegroundColor = eventArgs.Level.GetConsoleColor();
			Console.Write(log);
			Kon.ForegroundColor = ConsoleColor.Gray;
		}

		public void Dispose() { }
	}
}