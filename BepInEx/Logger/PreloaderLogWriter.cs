using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BepInEx.Logger
{
    public class PreloaderLogWriter : BaseLogger
    {
        public StringBuilder StringBuilder = new StringBuilder();

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

        public PreloaderLogWriter()
        {
            stdout = Console.Out;
            traceListener = new LoggerTraceListener(this);
        }

        public void Enable()
        {
            if (Enabled)
                return;

            Console.SetOut(this);
            Trace.Listeners.Add(traceListener);

            _enabled = true;
        }

        public void Disable()
        {
            if (!Enabled)
                return;

            Console.SetOut(stdout);
            Trace.Listeners.Remove(traceListener);

            _enabled = false;
        }

        public override void Write(char value)
        {
            StringBuilder.Append(value);
        }

        public override void Write(string value)
        {
            StringBuilder.Append(value);
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
