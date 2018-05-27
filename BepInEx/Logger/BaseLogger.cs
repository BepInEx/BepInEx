using System.IO;
using System.Text;

namespace BepInEx.Logger
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
        public static event EntryLoggedEventHandler EntryLogged;

        
        public LogLevel DisplayedLevels = LogLevel.All;



        public virtual void Log(LogLevel level, object entry)
        {
            if ((DisplayedLevels & level) != LogLevel.None)
            {
                EntryLogged?.Invoke(level, entry);
                WriteLine($"[{level}] {entry}");
            }
        }

        public virtual void Log(object entry)
        {
            Log(LogLevel.Message, entry);
        }
    }
}
