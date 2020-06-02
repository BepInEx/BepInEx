using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace BepInEx.Preloader
{
	public class PreloaderConsoleListener : ILogListener
	{
		public static List<LogEventArgs> LogEvents { get; } = new List<LogEventArgs>();

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

			ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
			ConsoleDirectWrite(eventArgs.ToStringLine());
			ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
		}

		public void ConsoleDirectWrite(string value)
		{
			StandardOut.Write(value);
		}

		public void ConsoleDirectWriteLine(string value)
		{
			StandardOut.WriteLine(value);
		}

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