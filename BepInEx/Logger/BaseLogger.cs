using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BepInEx.Logger
{
    public abstract class BaseLogger : TextWriter
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(true);

        public virtual void Log(LogLevel level, object entry)
        {
            WriteLine($"[{level}] {entry}");
        }

        public virtual void Log(object entry)
        {
            Log(LogLevel.Message, entry);
        }
    }
}
