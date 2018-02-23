namespace BepInEx
{
    /// <summary>
    /// A helper class to use for logging.
    /// </summary>
    public static class BepInLogger
    {
        /// <summary>
        /// The handler for a entry logged event.
        /// </summary>
        /// <param name="entry">The text element of the log itself.</param>
        /// <param name="show">Whether or not it should be dislpayed to the user.</param>
        public delegate void EntryLoggedEventHandler(string entry, bool show = false);

        /// <summary>
        /// The listener event for an entry being logged.
        /// </summary>
        public static event EntryLoggedEventHandler EntryLogged;

        /// <summary>
        /// Logs an entry to the logger, and any listeners are notified of the entry.
        /// </summary>
        /// <param name="entry">The text element of the log itself.</param>
        /// <param name="show">Whether or not it should be dislpayed to the user.</param>
        public static void Log(string entry, bool show = false)
        {
            EntryLogged?.Invoke(entry, show);
        }
    }
}
