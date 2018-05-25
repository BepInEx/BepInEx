using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx.Logger
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Fatal = 1,
        Error = 2,
        Warning = 4,
        Message = 8,
        Info = 16,
        Debug = 32,

        All = Fatal | Error | Warning | Message | Info | Debug
    }
}
