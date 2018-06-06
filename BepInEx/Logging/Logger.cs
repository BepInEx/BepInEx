using BepInEx.Logging;

namespace BepInEx
{
    public static class Logger
    {
        public static BaseLogger CurrentLogger { get; set; }

        /// <summary>
        /// The listener event for an entry being logged.
        /// </summary>
        public static event BaseLogger.EntryLoggedEventHandler EntryLogged;

        public static void Log(LogLevel level, object entry)
        {
            EntryLogged?.Invoke(level, entry);

            CurrentLogger?.Log(level, entry);
        }

        public static void SetLogger(BaseLogger logger)
        {
            CurrentLogger?.Dispose();

            CurrentLogger = logger;
        }
    }
}
