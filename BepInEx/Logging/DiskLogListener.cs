using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx.Configuration;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class DiskLogListener : ILogListener
	{
		protected LogLevel DisplayedLogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), ConfigConsoleDisplayedLevel.Value, true);

		protected TextWriter LogWriter = TextWriter.Synchronized(new StreamWriter(Path.Combine(Paths.BepInExRootPath, "LogOutput.log"), ConfigAppendLog.Value, Encoding.UTF8));

		protected Timer FlushTimer;

		public DiskLogListener()
		{
			FlushTimer = new Timer(o =>
			{
				LogWriter?.Flush();
			}, null, 2000, 2000);
		}

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (!ConfigWriteUnityLog.Value && eventArgs.Source is UnityLogSource)
				return;

			if (eventArgs.Level.GetHighestLevel() > DisplayedLogLevel)
				return;

			LogWriter.WriteLine($"[{eventArgs.Level,-7}:{((ILogSource)sender).SourceName,10}] {eventArgs.Data}");
		}

		public void Dispose()
		{
			FlushTimer.Dispose();
			LogWriter.Flush();
			LogWriter.Dispose();
		}

		~DiskLogListener()
		{
			Dispose();
		}

		private static readonly ConfigWrapper<string> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"DisplayedLogLevel",
			"Only displays the specified log level and above in the console output.",
			"Info");

		private static readonly ConfigWrapper<bool> ConfigWriteUnityLog = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"WriteUnityLog",
			"Include unity log messages in log file output.",
			false);

		private static readonly ConfigWrapper<bool> ConfigAppendLog = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"AppendLog",
			"Appends to the log file instead of overwriting, on game startup.",
			false);
	}
}