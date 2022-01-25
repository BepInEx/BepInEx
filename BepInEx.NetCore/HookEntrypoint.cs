using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;
using BepInEx.NetCore;
using BepInEx.NetLauncher.Shared;
using BepInEx.Preloader.Core;

internal class StartupHook
{
    public static List<string> ResolveDirectories = new();

    public static void Initialize()
    {
        var silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

        try
        {
            string filename, gameDirectory;

//#if DEBUG
//          filename =
//              Path.Combine(Directory.GetCurrentDirectory(),
//                           Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
//          ResolveDirectories.Add(Path.GetDirectoryName(filename));

//          // for debugging within VS
//          ResolveDirectories.Add(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
//#else
            filename = Process.GetCurrentProcess().MainModule.FileName;
            gameDirectory = Path.GetDirectoryName(filename);
            ResolveDirectories.Add(Path.Combine(gameDirectory, "BepInEx", "core"));
//#endif

            silentExceptionLog = Path.Combine(gameDirectory, silentExceptionLog);

            AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.RemoteResolve(ResolveDirectories);

            NetCorePreloaderRunner.OuterMain(filename);
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());

            Console.WriteLine("Unhandled exception");
            Console.WriteLine(ex);
        }
    }
}

namespace BepInEx.NetCore
{
    internal static class NetCorePreloaderRunner
    {
        internal static void PreloaderMain()
        {
            ConsoleManager.Initialize(false, false);

            ConsoleManager.CreateConsole();

            Logger.Listeners.Add(new ConsoleLogListener());

            try
            {
                NetCorePreloader.Start();
            }
            catch (Exception ex)
            {
                PreloaderLogger.Log.Log(LogLevel.Fatal, "Unhandled exception");
                PreloaderLogger.Log.Log(LogLevel.Fatal, ex);
            }
        }

        internal static void OuterMain(string filename)
        {
            PlatformUtils.SetPlatform();

            Paths.SetExecutablePath(filename);

            AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.LocalResolve;

            PreloaderMain();
        }
    }
}
