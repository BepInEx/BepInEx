using System;
using System.Runtime.CompilerServices;
using BepInEx.ConsoleUtil;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
    public class UnityLogWriter : BaseLogger
    {
		/// <summary>
		/// Writes a string specifically to the game output log.
		/// </summary>
		/// <param name="value">The value to write.</param>
        public void WriteToLog(string value)
        {
            UnityEngine.UnityLogWriter.WriteStringToUnityLog(value);
        }

        protected void InternalWrite(string value)
        {
            Console.Write(value);
            WriteToLog(value);
        }

	    /// <summary>
	    /// Logs an entry to the Logger instance.
	    /// </summary>
	    /// <param name="level">The level of the entry.</param>
	    /// <param name="entry">The textual value of the entry.</param>
        public override void Log(LogLevel level, object entry)
        {
            Kon.ForegroundColor = level.GetConsoleColor();
            base.Log(level, entry);
            Kon.ForegroundColor = ConsoleColor.Gray;
        }

        public override void WriteLine(string value) => InternalWrite($"{value}\r\n");
        public override void Write(char value) => InternalWrite(value.ToString());
        public override void Write(string value) => InternalWrite(value);
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
