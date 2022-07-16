using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;
using BepInEx.NET.CoreCLR;
using BepInEx.NET.Shared;
using BepInEx.Preloader.Core;

internal class StartupHook
{
    public static List<string> ResolveDirectories = new();

    public static string DoesNotExistPath = "_doesnotexist_.exe";

    public static void Initialize()
    {
        var silentExceptionLog = $"bepinex_preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

        try
        {
//#if DEBUG
//          filename =
//              Path.Combine(Directory.GetCurrentDirectory(),
//                           Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
//          ResolveDirectories.Add(Path.GetDirectoryName(filename));

//          // for debugging within VS
//          ResolveDirectories.Add(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
//#else
            
            var executableFilename = Process.GetCurrentProcess().MainModule.FileName;
            
            var assemblyFilename = TryDetermineAssemblyNameFromDotnet(executableFilename)
                                ?? TryDetermineAssemblyNameFromStubExecutable(executableFilename)
                                ?? TryDetermineAssemblyNameFromCurrentAssembly(executableFilename);

            string gameDirectory = null;

            if (assemblyFilename != null)
                gameDirectory = Path.GetDirectoryName(assemblyFilename);

            string bepinexCoreDirectory = null;

            if (gameDirectory != null)
                bepinexCoreDirectory = Path.Combine(gameDirectory, "BepInEx", "core");

            if (assemblyFilename == null || gameDirectory == null || !Directory.Exists(bepinexCoreDirectory))
            {
                throw new Exception("Could not determine game location, or BepInEx install location");
            }
            
            silentExceptionLog = Path.Combine(gameDirectory, silentExceptionLog);
            
            ResolveDirectories.Add(bepinexCoreDirectory);
//#endif

            AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.RemoteResolve(ResolveDirectories);

            NetCorePreloaderRunner.OuterMain(assemblyFilename);
        }
        catch (Exception ex)
        {
            string executableLocation = null;
            string arguments = null;

            try
            {
                executableLocation = Process.GetCurrentProcess().MainModule?.FileName;
                arguments = string.Join(' ', Environment.GetCommandLineArgs());
            }
            catch { }

            string exceptionString = $"Unhandled fatal exception\r\n" +
                                     $"Executable location: {executableLocation ?? "<null>"}\r\n" +
                                     $"Arguments: {arguments ?? "<null>"}\r\n" +
                                     $"{ex}";

            File.WriteAllText(silentExceptionLog, exceptionString);

            Console.WriteLine("Unhandled exception");
            Console.WriteLine($"Executable location: {executableLocation ?? "<null>"}");
            Console.WriteLine($"Arguments: {arguments ?? "<null>"}");
            Console.WriteLine(ex);
        }
    }

    private static string TryDetermineAssemblyNameFromDotnet(string executableFilename)
    {
        if (Path.GetFileNameWithoutExtension(executableFilename) == "dotnet")
        {
            // We're in a special setup that uses dotnet directly to start a .dll, instead of a .exe that launches dotnet implicitly

            var args = Environment.GetCommandLineArgs();

            foreach (var arg in args)
            {
                if (!arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                 && !arg.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(arg))
                {
                    return Path.GetFullPath(arg);
                }
            }
        }

        return null;
    }

    private static string TryDetermineAssemblyNameFromStubExecutable(string executableFilename)
    {
        string dllFilename = Path.ChangeExtension(executableFilename, ".dll");

        if (File.Exists(dllFilename))
            return dllFilename;

        return null;
    }

    private static string TryDetermineAssemblyNameFromCurrentAssembly(string executableFilename)
    {
        string assemblyLocation = typeof(StartupHook).Assembly.Location.Replace('/', Path.DirectorySeparatorChar);

        string coreFolderPath = Path.GetDirectoryName(assemblyLocation);

        if (coreFolderPath == null)
            return null; // throw new Exception("Could not find a valid path to the BepInEx directory");

        string gameDirectory = Path.GetDirectoryName(Path.GetDirectoryName(coreFolderPath));

        if (gameDirectory == null)
            return null; // throw new Exception("Could not find a valid path to the game directory");

        return Path.Combine(gameDirectory, DoesNotExistPath);
    }
}

namespace BepInEx.NET.CoreCLR
{
    internal static class NetCorePreloaderRunner
    {
        internal static void PreloaderMain()
        {
            ConsoleManager.Initialize(false, true);

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
