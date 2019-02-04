using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.ConsoleUtil;

namespace BepInEx.Logging
{
	/// <summary>
	/// A log writer specific to the <see cref="Preloader"/>.
	/// </summary>
	/// <inheritdoc cref="BaseLogger"/>
    public class PreloaderLogWriter : BaseLogger
    {
		/// <summary>
		/// The <see cref="System.Text.StringBuilder"/> which contains all logged entries, so it may be passed onto another log writer.
		/// </summary>
        public StringBuilder StringBuilder { get; protected set; } = new StringBuilder();

		/// <summary>
		/// Whether or not the log writer is redirecting console output, so it can be logged.
		/// </summary>
        public bool IsRedirectingConsole { get; protected set; }

        protected TextWriter stdout;
        protected LoggerTraceListener traceListener;

        private bool _enabled = false;

		/// <summary>
		/// Whether or not the log writer is writing and/or redirecting.
		/// </summary>
        public bool Enabled {
            get => _enabled;
            set
            {
                if (value)
                    Enable();
                else
                    Disable();
            }
        }
		
		/// <param name="redirectConsole">Whether or not to redirect the console standard output.</param>
        public PreloaderLogWriter(bool redirectConsole)
        {
            IsRedirectingConsole = redirectConsole;
            
            stdout = Console.Out;
            traceListener = new LoggerTraceListener(this);
        }

		/// <summary>
		/// Enables the log writer.
		/// </summary>
        public void Enable()
        {
            if (_enabled)
                return;

            if (IsRedirectingConsole)
                Console.SetOut(this);
            else
                Console.SetOut(Null);

            Trace.Listeners.Add(traceListener);

            _enabled = true;
        }

		/// <summary>
		/// Disables the log writer.
		/// </summary>
        public void Disable()
        {
            if (!_enabled)
                return;

            Console.SetOut(stdout);

            Trace.Listeners.Remove(traceListener);

            _enabled = false;
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

        public override void Write(char value)
        {
            StringBuilder.Append(value);

            stdout.Write(value);
        }

        public override void Write(string value)
        {
            StringBuilder.Append(value);

            stdout.Write(value);
        }

        protected override void Dispose(bool disposing)
        {
            Disable();
            StringBuilder.Length = 0;
            
            traceListener?.Dispose();
            traceListener = null;
        }

        public override string ToString()
        {
            return StringBuilder.ToString().Trim();
        }
    }
}
