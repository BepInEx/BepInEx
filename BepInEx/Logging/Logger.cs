using BepInEx.Logging;

namespace BepInEx
{
	/// <summary>
	/// A static <see cref="BaseLogger"/> instance.
	/// </summary>
    public static class Logger
    {
		/// <summary>
		/// The current instance of a <see cref="BaseLogger"/> that is being used.
		/// </summary>
        public static BaseLogger CurrentLogger { get; set; }

        /// <summary>
        /// The listener event for an entry being logged.
        /// </summary>
        public static event BaseLogger.EntryLoggedEventHandler EntryLogged;

		/// <summary>
		/// Logs an entry to the current logger instance.
		/// </summary>
		/// <param name="level">The level of the entry.</param>
		/// <param name="entry">The textual value of the entry.</param>
        public static void Log(LogLevel level, object entry)
        {
            EntryLogged?.Invoke(level, entry);

            CurrentLogger?.Log(level, entry);
        }

		/// <summary>
		/// Sets the instance being used by the static <see cref="Logger"/> class.
		/// </summary>
		/// <param name="logger">The instance to use in the static class.</param>
        public static void SetLogger(BaseLogger logger)
        {
            CurrentLogger?.Dispose();

            CurrentLogger = logger;
        }
    }
}
