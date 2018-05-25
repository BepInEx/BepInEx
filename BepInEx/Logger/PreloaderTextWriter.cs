using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BepInEx.Logger
{
    public class PreloaderTextWriter : BaseLogger
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(true);

        public StringBuilder StringBuilder = new StringBuilder();

        protected TextWriter stdout;
        protected TextWriterTraceListener traceListener;

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

        public PreloaderTextWriter()
        {
            stdout = Console.Out;
            traceListener = new TextWriterTraceListener(this, "Preloader");
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

        public override void Close()
        {
            Disable();
            StringBuilder.Length = 0;
        }

        public override string ToString()
        {
            return StringBuilder.ToString().Trim();
        }
    }
}
