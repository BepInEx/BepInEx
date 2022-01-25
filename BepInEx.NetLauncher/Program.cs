using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;
using BepInEx.NetLauncher.Shared;
using BepInEx.Preloader.Core;

namespace BepInEx.NetLauncher;

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
        var silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

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
            
            AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.RemoteResolve(ResolveDirectories);

            NetPreloaderRunner.OuterMain(args, filename);
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());

            Console.WriteLine("Unhandled exception");
            Console.WriteLine(ex);
            ReadExit();
        }
    }
}

internal static class NetPreloaderRunner
{
    internal static void PreloaderMain(string[] args)
    {
        Logger.Listeners.Add(new ConsoleLogListener());

        ConsoleManager.Initialize(true, false);

        try
        {
            NetPreloader.Start(args);
        }
        catch (Exception ex)
        {
            PreloaderLogger.Log.Log(LogLevel.Fatal, "Unhandled exception");
            PreloaderLogger.Log.Log(LogLevel.Fatal, ex);
            Program.ReadExit();
        }
    }

    internal static void OuterMain(string[] args, string filename)
    {
        PlatformUtils.SetPlatform();

        Paths.SetExecutablePath(filename);

        AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.LocalResolve;

        PreloaderMain(args);
    }
}
