using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BepInEx.Common
{
    public static class Utility
    {
        public static string ExecutingDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "");
        public static string PluginsDirectory => Path.Combine(ExecutingDirectory, "BepInEx");

        //shamelessly stolen from Rei
        public static string CombinePaths(params string[] parts) => parts.Aggregate(Path.Combine);
    }
}
