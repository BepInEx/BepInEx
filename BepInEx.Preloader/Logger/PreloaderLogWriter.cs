using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.ConsoleUtil;
using BepInEx.Logging;

namespace BepInEx.Preloader
{
	public class PreloaderConsoleListener : ILogListener
	{
		public List<LogEventArgs> LogEvents { get; } = new List<LogEventArgs>();
		protected StringBuilder LogBuilder = new StringBuilder();

		public static TextWriter StandardOut { get; set; }
		protected PreloaderConsoleSource LoggerSource { get; set; }


		public PreloaderConsoleListener(bool redirectConsole)
		{
			StandardOut = Console.Out;

			if (redirectConsole)
			{
				LoggerSource = new PreloaderConsoleSource();

				Logger.Sources.Add(LoggerSource);
				Console.SetOut(LoggerSource);
			}
		}

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			LogEvents.Add(eventArgs);

			string log = $"[{eventArgs.Level,-7}:{((ILogSource)sender).SourceName,10}] {eventArgs.Data}\r\n";

			LogBuilder.Append(log);

			Kon.ForegroundColor = eventArgs.Level.GetConsoleColor();
			ConsoleDirectWrite(log);
			Kon.ForegroundColor = ConsoleColor.Gray;
		}

		public void ConsoleDirectWrite(string value)
		{
			StandardOut.Write(value);
		}

		public void ConsoleDirectWriteLine(string value)
		{
			StandardOut.WriteLine(value);
		}

		public override string ToString() => LogBuilder.ToString();

		public void Dispose()
		{
			if (LoggerSource != null)
			{
				Console.SetOut(StandardOut);
				Logger.Sources.Remove(LoggerSource);
				LoggerSource.Dispose();
				LoggerSource = null;
			}
		}
	}

	public class PreloaderConsoleSource : TextWriter, ILogSource
	{
		public override Encoding Encoding { get; } = Console.OutputEncoding;

		public string SourceName { get; } = "BepInEx Preloader";

		public event EventHandler<LogEventArgs> LogEvent;

		public override void Write(object value)
			=> LogEvent?.Invoke(this, new LogEventArgs(value, LogLevel.Info, this));

		public override void Write(string value)
			=> Write((object)value);

		public override void WriteLine() { }

		public override void WriteLine(object value)
			=> Write(value);

		public override void WriteLine(string value)
			=> Write(value);
	}
}