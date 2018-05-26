using System.Diagnostics;

namespace BepInEx.Logger
{
    public class LoggerTraceListener : TraceListener
    {
        public BaseLogger Logger;

        public LoggerTraceListener(BaseLogger logger)
        {
            Logger = logger;
        }

        public override void Write(string message)
        {
            Logger.Write(message);
        }

        public override void WriteLine(string message)
        {
            Logger.WriteLine(message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
            => TraceEvent(eventCache, source, eventType, id, string.Format(format, args));

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            LogLevel level;

            switch (eventType)
            {
                case TraceEventType.Critical:
                    level = LogLevel.Fatal;
                    break;
                case TraceEventType.Error:
                    level = LogLevel.Error;
                    break;
                case TraceEventType.Warning:
                    level = LogLevel.Warning;
                    break;
                case TraceEventType.Information:
                    level = LogLevel.Info;
                    break;
                case TraceEventType.Verbose:
                default:
                    level = LogLevel.Debug;
                    break;
            }

            Logger.Log(level, $"{source} : {message}");
        }
    }
}
