using System.Diagnostics;
using System.Linq;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core.Logging
{
	public static class ChainloaderLogHelper
	{
		public static void PrintLogInfo(ManualLogSource log)
		{
			string consoleTile = $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
			log.LogMessage(consoleTile);

			if (ConsoleManager.ConsoleActive)
				ConsoleManager.SetConsoleTitle(consoleTile);

			//See BuildInfoAttribute for more information about this section.
			object[] attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

			if (attributes.Length > 0)
			{
				var attribute = (BuildInfoAttribute)attributes[0];
				log.LogMessage(attribute.Info);
			}
		}

		public static void RewritePreloaderLogs()
		{
			if (PreloaderConsoleListener.LogEvents == null || PreloaderConsoleListener.LogEvents.Count == 0)
				return;

			// Temporarily disable the console log listener as we replay the preloader logs
			var logListener = Logger.Listeners.FirstOrDefault(logger => logger is ConsoleLogListener);

			if (logListener != null)
				Logger.Listeners.Remove(logListener);

			foreach (var preloaderLogEvent in PreloaderConsoleListener.LogEvents)
			{
				PreloaderLogger.Log.Log(preloaderLogEvent.Level, $"[{ preloaderLogEvent.Source.SourceName,10}] { preloaderLogEvent.Data}");
			}

			if (logListener != null)
				Logger.Listeners.Add(logListener);
		}
	}
}
