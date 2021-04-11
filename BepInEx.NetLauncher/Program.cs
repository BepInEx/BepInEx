using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Preloader.Core;

namespace BepInEx.NetLauncher
{
    internal static class NetPreloaderRunner
    {
        internal static void PreloaderMain(string[] args)
        {
            try
            {
                Logger.Listeners.Add(new ConsoleLogListener());

                ConsoleManager.Initialize(true);

                NetPreloader.Start(args);
            }
            catch (Exception ex)
            {
                PreloaderLogger.Log.LogFatal("Unhandled exception");
                PreloaderLogger.Log.LogFatal(ex);
                Program.ReadExit();
            }
        }

        internal static void OuterMain(string[] args, string filename)
        {
            Paths.SetExecutablePath(filename);

            AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

            PreloaderMain(args);
        }

        private static Assembly LocalResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);

            var foundAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                         .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

            if (foundAssembly != null)
                return foundAssembly;

            if (LocalUtility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
             || LocalUtility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
             || LocalUtility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
                return foundAssembly;

            return null;
        }
    }

    internal class Program
    {
        public static List<string> ResolveDirectories { get; set; } = new()
        {
            "C:\\Windows\\Microsoft.NET\\assembly\\GAC_32\\Microsoft.Xna.Framework.Game\\v4.0_4.0.0.0__842cf8be1de50553\\"
        };

        internal static void ReadExit()
        {
            Console.WriteLine("Press enter to exit...");
            Console.ReadLine();
            Environment.Exit(-1);
        }

        private static void Main(string[] args)
        {
            try
            {
                string filename;

#if DEBUG
                filename =
                    Path.Combine(Directory.GetCurrentDirectory(),
                                 Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
                ResolveDirectories.Add(Path.GetDirectoryName(filename));

                // for debugging within VS
                ResolveDirectories.Add(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
#else
                filename = Process.GetCurrentProcess().MainModule.FileName;
                ResolveDirectories.Add(Path.Combine(Path.GetDirectoryName(filename), "BepInEx", "core"));
#endif


                AppDomain.CurrentDomain.AssemblyResolve += RemoteResolve;

                NetPreloaderRunner.OuterMain(args, filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unhandled exception");
                Console.WriteLine(ex);
                ReadExit();
            }
        }

        private static Assembly RemoteResolve(object sender, ResolveEventArgs reference)
        {
            var assemblyName = new AssemblyName(reference.Name);

            foreach (var directory in ResolveDirectories)
            {
                if (!Directory.Exists(directory))
                    continue;

                var potentialDirectories = new List<string> { directory };

                potentialDirectories.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));

                var potentialFiles = potentialDirectories.Select(x => Path.Combine(x, $"{assemblyName.Name}.dll"))
                                                         .Concat(potentialDirectories.Select(x =>
                                                                     Path
                                                                         .Combine(x,
                                                                             $"{assemblyName.Name}.exe")));

                foreach (var path in potentialFiles)
                {
                    if (!File.Exists(path))
                        continue;

                    Assembly assembly;

                    try
                    {
                        assembly = Assembly.LoadFrom(path);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }

                    if (assembly.GetName().Name == assemblyName.Name)
                        return assembly;
                }
            }

            return null;
        }
    }
}
