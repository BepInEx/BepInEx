using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class DiskLogListener : ILogListener
	{
		public LogLevel DisplayedLogLevel { get; set; }

		public TextWriter LogWriter { get; protected set; }

		public Timer FlushTimer { get; protected set; }

		public DiskLogListener(string localPath, LogLevel displayedLogLevel = LogLevel.Info, bool appendLog = false)
		{
			DisplayedLogLevel = displayedLogLevel;

			int counter = 1;

			FileStream fileStream;

			while (!Utility.TryOpenFileStream(Path.Combine(Paths.BepInExRootPath, localPath), appendLog ? FileMode.Append : FileMode.Create, out fileStream, share: FileShare.Read))
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

		public static HashSet<string> BlacklistedSources = new HashSet<string>();

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (BlacklistedSources.Contains(eventArgs.Source.SourceName))
				return;

			if ((eventArgs.Level & DisplayedLogLevel) == 0)
				return;

			LogWriter.WriteLine(eventArgs.ToString());
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
	}
}