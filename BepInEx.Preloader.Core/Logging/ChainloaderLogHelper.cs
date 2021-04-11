using System.Linq;
using BepInEx.Logging;
using BepInEx.Shared;
using MonoMod.Utils;

namespace BepInEx.Preloader.Core.Logging
{
    public static class ChainloaderLogHelper
    {
        public static void PrintLogInfo(ManualLogSource log)
        {
            var consoleTitle = $"BepInEx {Paths.BepInExVersion} - {Paths.ProcessName}";
            log.LogMessage(consoleTitle);

            if (ConsoleManager.ConsoleActive)
                ConsoleManager.SetConsoleTitle(consoleTitle);

            //See BuildInfoAttribute for more information about this section.
            var attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

            if (attributes.Length > 0)
            {
                var attribute = (BuildInfoAttribute) attributes[0];
                log.LogMessage(attribute.Info);
            }

            Logger.LogInfo($"System platform: {PlatformHelper.Current}");
        }

        public static void RewritePreloaderLogs()
        {
            if (PreloaderConsoleListener.LogEvents == null || PreloaderConsoleListener.LogEvents.Count == 0)
                return;

            // Temporarily disable the console log listener (if there is one from preloader) as we replay the preloader logs
            var logListener = Logger.Listeners.FirstOrDefault(logger => logger is ConsoleLogListener);

            if (logListener != null)
                Logger.Listeners.Remove(logListener);

            foreach (var preloaderLogEvent in PreloaderConsoleListener.LogEvents)
                Logger.InternalLogEvent(PreloaderLogger.Log, preloaderLogEvent);

            if (logListener != null)
                Logger.Listeners.Add(logListener);
        }
    }
}
