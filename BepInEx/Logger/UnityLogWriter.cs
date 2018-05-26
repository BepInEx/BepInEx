using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace BepInEx.Logger
{
    public class UnityLogWriter : BaseLogger
    {
        public void WriteToUnity(string value)
        {
            Console.Write(value);
            UnityEngine.UnityLogWriter.WriteStringToUnityLog(value);
        }

        public override void WriteLine(string value) => WriteToUnity($"{value}\r\n");
        public override void Write(char value) => WriteToUnity(value.ToString());
        public override void Write(string value) => WriteToUnity(value);
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
