using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Common;
using BepInEx.Logging;
using UnityInjector.ConsoleUtil;

namespace BepInEx.Bootstrap
{
    /// <summary>
    /// The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
    /// </summary>
    public static class Preloader
    {
        #region Path Properties

        private static string executablePath;

        /// <summary>
        /// The path of the currently executing program BepInEx is encapsulated in.
        /// </summary>
        public static string ExecutablePath
        {
            get => executablePath;
            set
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
        /// The path to the core BepInEx DLL.
        /// </summary>
        public static string BepInExAssemblyPath { get; private set; }

        /// <summary>
        /// The directory that the core BepInEx DLLs reside in.
        /// </summary>
        public static string BepInExAssemblyDirectory { get; private set; }

        /// <summary>
        /// The name of the currently executing process.
        /// </summary>
        public static string ProcessName { get; } = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);

        /// <summary>
        /// The directory that the currently executing process resides in.
        /// </summary>
        public static string GameRootPath { get; private set; }

        /// <summary>
        /// The path to the Managed folder of the currently running Unity game.
        /// </summary>
        public static string ManagedPath { get; private set; }

        /// <summary>
        /// The path to the main BepInEx folder.
        /// </summary>
        public static string PluginPath { get; private set; }

        /// <summary>
        /// The path to the patcher plugin folder which resides in the BepInEx folder.
        /// </summary>
        public static string PatcherPluginPath { get; private set; }

        #endregion

        /// <summary>
        /// The log writer that is specific to the preloader.
        /// </summary>
        public static PreloaderLogWriter PreloaderLog { get; private set; }


        /// <summary>
        /// Safely retrieves a boolean value from the config. Returns false if not able to retrieve safely.
        /// </summary>
        /// <param name="key">The key to retrieve from the config.</param>
        /// <param name="defaultValue">The default value to both return and set if the key does not exist in the config.</param>
        /// <returns>The value of the key if found in the config, or the default value specified if not found, or false if it was unable to safely retrieve the value from the config.</returns>
        private static bool SafeGetConfigBool(string key, string defaultValue)
        {
            try
            {
                string result = Config.GetEntry(key, defaultValue);

                return bool.Parse(result);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Allocates a console window for use by BepInEx safely.
        /// </summary>
        internal static void AllocateConsole()
        {
            bool console = SafeGetConfigBool("console", "false");
            bool shiftjis = SafeGetConfigBool("console-shiftjis", "false");

            if (console)
            {
                try
                {
                    ConsoleWindow.Attach();

                    uint encoding = (uint) Encoding.UTF8.CodePage;

                    if (shiftjis)
                        encoding = 932;

                    ConsoleEncoding.ConsoleCodePage = encoding;
                    Console.OutputEncoding = ConsoleEncoding.GetEncoding(encoding);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "Failed to allocate console!");
                    Logger.Log(LogLevel.Error, ex);
                }
            }
        }


        /// <summary>
        /// The main entrypoint of BepInEx, called from Doorstop.
        /// </summary>
        /// <param name="args">The arguments passed in from Doorstop. First argument is the path of the currently executing process.</param>
        public static void Main(string[] args)
        {
            ExecutablePath = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

            try
            {
                AllocateConsole();

                PreloaderLog = new PreloaderLogWriter(SafeGetConfigBool("preloader-logconsole", "false"));
                PreloaderLog.Enabled = true;

                string consoleTile =
                        $"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
                ConsoleWindow.Title = consoleTile;

                Logger.SetLogger(PreloaderLog);

                PreloaderLog.WriteLine(consoleTile);
                Logger.Log(LogLevel.Message, "Preloader started");

                PreloaderPatchManager.Run();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Fatal, "Could not run preloader!");
                Logger.Log(LogLevel.Fatal, ex);

                PreloaderLog.Enabled = false;

                try
                {
                    UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
                    Console.Write(PreloaderLog);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
                                      PreloaderLog.ToString());

                    PreloaderLog.Dispose();
                }
            }
        }

        /// <summary>
        /// A handler for <see cref="AppDomain"/>.AssemblyResolve to perform some special handling.
        /// <para>
        /// It attempts to check currently loaded assemblies (ignoring the version), and then checks the BepInEx/core path, BepInEx/patchers path and the BepInEx folder, all in that order.
        /// </para>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static Assembly LocalResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);

            var foundAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

            if (foundAssembly != null)
                return foundAssembly;

            if (Utility.TryResolveDllAssembly(assemblyName, BepInExAssemblyDirectory, out foundAssembly)
                || Utility.TryResolveDllAssembly(assemblyName, PatcherPluginPath, out foundAssembly)
                || Utility.TryResolveDllAssembly(assemblyName, PluginPath, out foundAssembly))
                return foundAssembly;

            return null;
        }
    }
}