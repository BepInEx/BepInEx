using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx.Common;

namespace BepInEx
{
    /// <summary>
    ///     Paths used by BepInEx
    /// </summary>
    public static class Paths
    {
        private static string executablePath;

        /// <summary>
        ///     The directory that the core BepInEx DLLs reside in.
        /// </summary>
        public static string BepInExAssemblyDirectory { get; private set; }

        /// <summary>
        ///     The path to the core BepInEx DLL.
        /// </summary>
        public static string BepInExAssemblyPath { get; private set; }

        /// <summary>
        ///     The path of the currently executing program BepInEx is encapsulated in.
        /// </summary>
        public static string ExecutablePath
        {
            get => executablePath;
            internal set
            {
                executablePath = value;
                GameRootPath = Path.GetDirectoryName(executablePath);
                ManagedPath = Utility.CombinePaths(GameRootPath, $"{ProcessName}_Data", "Managed");
                PluginPath = Utility.CombinePaths(GameRootPath, "BepInEx");
                PatcherPluginPath = Utility.CombinePaths(GameRootPath, "BepInEx", "patchers");
                BepInExAssemblyDirectory = Utility.CombinePaths(GameRootPath, "BepInEx", "core");
                BepInExAssemblyPath =
                        Utility.CombinePaths(BepInExAssemblyDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.dll");
            }
        }

        /// <summary>
        ///     The directory that the currently executing process resides in.
        /// </summary>
        public static string GameRootPath { get; private set; }

        /// <summary>
        ///     The path to the Managed folder of the currently running Unity game.
        /// </summary>
        public static string ManagedPath { get; private set; }

        /// <summary>
        ///     The path to the patcher plugin folder which resides in the BepInEx folder.
        /// </summary>
        public static string PatcherPluginPath { get; private set; }

        /// <summary>
        ///     The path to the main BepInEx folder.
        /// </summary>
        public static string PluginPath { get; private set; }

        /// <summary>
        ///     The name of the currently executing process.
        /// </summary>
        public static string ProcessName { get; } = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);
    }
}