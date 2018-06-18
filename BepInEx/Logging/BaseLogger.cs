using System;
using System.IO;
using System.Text;

namespace BepInEx.Logging
{
	/// <summary>
	/// The base implementation of a logging class.
	/// </summary>
    public abstract class BaseLogger : TextWriter
    {
	    protected BaseLogger()
	    {
		    DisplayedLevels = GetDefaultLogLevel();
	    }

		/// <summary>
		/// The encoding that the underlying text writer should use. Defaults to UTF-8 BOM.
		/// </summary>
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

		/// <summary>
		/// Retrieves the default log level to use for this logger.
		/// </summary>
		/// <returns>The default log level to use.</returns>
	    protected virtual LogLevel GetDefaultLogLevel()
		{
			LogLevel lowest;

		    try
		    {
			    lowest = (LogLevel)Enum.Parse(typeof(LogLevel), Config.GetEntry("logger-displayed-levels", "All"));
		    }
		    catch
		    {
			    return LogLevel.All;
		    }

		    if (lowest == LogLevel.None)
			    return LogLevel.None;

		    LogLevel value = lowest;

		    foreach (LogLevel e in Enum.GetValues(typeof(LogLevel)))
		    {
			    if (lowest > e)
				    value |= e;
		    }

		    return value;
	    }

        /// <summary>
		/// A filter which is used to specify which log levels are not ignored by the logger.
		/// </summary>
        public LogLevel DisplayedLevels { get; set; }

        private readonly object logLockObj = new object();

		/// <summary>
		/// Logs an entry to the Logger instance.
		/// </summary>
		/// <param name="level">The level of the entry.</param>
		/// <param name="entry">The textual value of the entry.</param>
        public virtual void Log(LogLevel level, object entry)
        {
            if ((DisplayedLevels & level) != LogLevel.None)
            {
                lock (logLockObj)
                {
                    EntryLogged?.Invoke(level, entry);
                    WriteLine($"[{level.GetHighestLevel()}] {entry}");
                }
            }
        }

		/// <summary>
		/// Logs an entry to the Logger instance, with a <see cref="LogLevel"/> of Message.
		/// </summary>
		/// <param name="entry">The text value of this log entry.</param>
        public virtual void Log(object entry)
        {
            Log(LogLevel.Message, entry);
        }
    }
}
