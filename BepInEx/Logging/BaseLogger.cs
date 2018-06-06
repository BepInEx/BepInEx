using System.IO;
using System.Text;

namespace BepInEx.Logging
{
    public abstract class BaseLogger : TextWriter
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(true);


        /// <summary>
        /// The handler for a entry logged event.
        /// </summary>
        /// <param name="entry">The text element of the log itself.</param>
        /// <param name="show">Whether or not it should be dislpayed to the user.</param>
        public delegate void EntryLoggedEventHandler(LogLevel level, object entry);

        /// <summary>
        /// The listener event for an entry being logged.
        /// </summary>
        public event EntryLoggedEventHandler EntryLogged;

        
        public LogLevel DisplayedLevels = LogLevel.All;

        private object logLockObj = new object();

        public virtual void Log(LogLevel level, object entry)
        {
            if ((DisplayedLevels & level) != LogLevel.None)
            {
                lock (logLockObj)
                {
                    EntryLogged?.Invoke(level, entry);
                    WriteLine($"[{level}] {entry}");
                }
            }
        }

        public virtual void Log(object entry)
        {
            Log(LogLevel.Message, entry);
        }
    }
}
