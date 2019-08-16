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
		protected TextWriter LogWriter { get; set; }

		protected Timer FlushTimer { get; set; }

		public DiskLogListener()
		{
			int counter = 1;
			string localPath = "LogOutput.log";

			FileStream fileStream;

			while (!Utility.TryOpenFileStream(Path.Combine(Paths.BepInExRootPath, localPath), ConfigAppendLog.Value ? FileMode.Append : FileMode.Create, out fileStream, share: FileShare.Read))
			{
				if (counter == 5)
				{
					Logger.LogError("Couldn't open a log file for writing. Skipping log file creation");

					return;
				}

				Logger.LogWarning($"Couldn't open log file '{localPath}' for writing, trying another...");

				localPath = $"LogOutput.log.{counter++}";
			}

			LogWriter = TextWriter.Synchronized(new StreamWriter(fileStream, Encoding.UTF8));


			FlushTimer = new Timer(o => { LogWriter?.Flush(); }, null, 2000, 2000);
		}

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (!ConfigWriteUnityLog.Value && eventArgs.Source is UnityLogSource)
				return;

			if (eventArgs.Level.GetHighestLevel() > ConfigConsoleDisplayedLevel.Value)
				return;

			LogWriter.WriteLine($"[{eventArgs.Level,-7}:{((ILogSource)sender).SourceName,10}] {eventArgs.Data}");
		}

		public void Dispose()
		{
			FlushTimer?.Dispose();
			LogWriter?.Flush();
			LogWriter?.Dispose();
		}

		~DiskLogListener()
		{
			Dispose();
		}

		private static readonly ConfigWrapper<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk", "DisplayedLogLevel",
			LogLevel.Info,
			new ConfigDescription("Only displays the specified log level and above in the console output."));

		private static readonly ConfigWrapper<bool> ConfigWriteUnityLog = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk", "WriteUnityLog",
			false,
			new ConfigDescription("Include unity log messages in log file output."));

		private static readonly ConfigWrapper<bool> ConfigAppendLog = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk", "AppendLog",
			false,
			new ConfigDescription("Appends to the log file instead of overwriting, on game startup."));
	}
}