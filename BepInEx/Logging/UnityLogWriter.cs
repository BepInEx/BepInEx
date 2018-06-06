using System;
using System.Runtime.CompilerServices;
using BepInEx.ConsoleUtil;

namespace BepInEx.Logging
{
    public class UnityLogWriter : BaseLogger
    {
        public void WriteToLog(string value)
        {
            UnityEngine.UnityLogWriter.WriteStringToUnityLog(value);
        }

        protected void InternalWrite(string value)
        {
            Console.Write(value);
            WriteToLog(value);
        }

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
