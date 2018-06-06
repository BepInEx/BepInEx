using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx.ConsoleUtil;

namespace BepInEx.Logging
{
    public class PreloaderLogWriter : BaseLogger
    {
        public StringBuilder StringBuilder { get; protected set; } = new StringBuilder();

        public bool IsRedirectingConsole { get; protected set; }

        protected TextWriter stdout;
        protected LoggerTraceListener traceListener;

        private bool _enabled = false;
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

        public PreloaderLogWriter(bool redirectConsole)
        {
            IsRedirectingConsole = redirectConsole;
            
            stdout = Console.Out;
            traceListener = new LoggerTraceListener(this);
        }

        public void Enable()
        {
            if (_enabled)
                return;

            if (IsRedirectingConsole)
                Console.SetOut(this);
            else
                Console.SetOut(TextWriter.Null);

            Trace.Listeners.Add(traceListener);

            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
                return;
            
            Console.SetOut(stdout);

            Trace.Listeners.Remove(traceListener);

            _enabled = false;
        }

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
