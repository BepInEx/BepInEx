using BepInEx.Logging;

namespace BepInEx
{
    public static class Logger
    {
        public static BaseLogger CurrentLogger { get; set; }

        public static void Log(LogLevel level, object entry)
        {
            CurrentLogger?.Log(level, entry);
        }

        public static void SetLogger(BaseLogger logger)
        {
            CurrentLogger?.Dispose();

            CurrentLogger = logger;
        }
    }
}
