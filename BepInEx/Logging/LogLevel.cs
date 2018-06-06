using System;
using System.Linq;

namespace BepInEx.Logging
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Fatal = 1,
        Error = 2,
        Warning = 4,
        Message = 8,
        Info = 16,
        Debug = 32,
        All = Fatal | Error | Warning | Message | Info | Debug
    }

    public static class LogLevelExtensions
    {
        public static LogLevel GetHighestLevel(this LogLevel levels)
        {
            var enums = Enum.GetValues(typeof(LogLevel));
            Array.Sort(enums);

            foreach (LogLevel e in enums)
            {
                if ((levels & e) != LogLevel.None)
                    return e;
            }

            return LogLevel.None;
        }

        public static ConsoleColor GetConsoleColor(this LogLevel level)
        {
            level = GetHighestLevel(level);

            switch (level)
            {
                case LogLevel.Fatal:
                    return ConsoleColor.Red;
                case LogLevel.Error:
                    return ConsoleColor.DarkRed;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Message:
                    return ConsoleColor.White;
                case LogLevel.Info:
                default:
                    return ConsoleColor.Gray;
                case LogLevel.Debug:
                    return ConsoleColor.DarkGray;
            }
        }
    }
}
