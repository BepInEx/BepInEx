using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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

            if (IsRedirectingConsole)
                stdout = Console.Out;

            traceListener = new LoggerTraceListener(this);
        }

        public void Enable()
        {
            if (Enabled)
                return;

            if (IsRedirectingConsole)
                Console.SetOut(this);

            Trace.Listeners.Add(traceListener);

            _enabled = true;
        }

        public void Disable()
        {
            if (!Enabled)
                return;

            if (IsRedirectingConsole)
                Console.SetOut(stdout);

            Trace.Listeners.Remove(traceListener);

            _enabled = false;
        }

        public override void Write(char value)
        {
            StringBuilder.Append(value);

            if (IsRedirectingConsole)
                stdout.Write(value);
            else
                Console.Write(value);
        }

        public override void Write(string value)
        {
            StringBuilder.Append(value);

            if (IsRedirectingConsole)
                stdout.Write(value);
            else
                Console.Write(value);
        }

        protected override void Dispose(bool disposing)
        {
            Disable();
            StringBuilder.Length = 0;

            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return StringBuilder.ToString().Trim();
        }
    }
}
