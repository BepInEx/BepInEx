using System;
using System.Runtime.CompilerServices;
using BepInEx.ConsoleUtil;

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
        /// <param name="entry">The text element of the log itself. Uses .ToString().</param>
        /// <param name="show">Whether or not it should be dislpayed to the user.</param>
        /// <param name="color">The color of the text to show in the console.</param>
        public static void Log(object entry, bool show = false, ConsoleColor color = ConsoleColor.Gray)
        {
            Log(entry.ToString(), show, color);
        }

        /// <summary>
        /// Logs an entry to the logger, and any listeners are notified of the entry.
        /// </summary>
        /// <param name="entry">The text element of the log itself.</param>
        /// <param name="show">Whether or not it should be dislpayed to the user.</param>
        /// <param name="color">The color of the text to show in the console.</param>
        public static void Log(string entry, bool show = false, ConsoleColor color = ConsoleColor.Gray)
        {
            UnityEngine.UnityLogWriter.WriteStringToUnityLog($"BEPIN - {entry}\r\n");

            Kon.ForegroundColor = color;
            Console.WriteLine(entry);

            EntryLogged?.Invoke(entry, show);
        }
    }
}

namespace UnityEngine
{
    internal sealed class UnityLogWriter
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void WriteStringToUnityLog(string s);
    }
}