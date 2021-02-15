using BepInEx.Logging;

namespace BepInEx.Core.Logging
{
    /// <summary>
    ///     Logs entries to StdOut, for platforms that spawn a console instance and hijack the original output stream.
    /// </summary>
    public class StdOutLogListener : ILogListener
    {
        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            ConsoleManager.StandardOutStream?.Write(eventArgs.ToStringLine());
        }

        public void Dispose() { }
    }
}
