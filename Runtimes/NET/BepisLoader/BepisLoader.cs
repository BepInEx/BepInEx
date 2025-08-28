using BepInEx;
using BepInEx.Core;
using BepInEx.Logging;
using BepInEx.NET.Shared;
using BepInEx.Preloader.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace BepisLoader;

internal class BepisLoader
{
    public static List<string> ResolveDirectories = new();

    public static string DoesNotExistPath = "_doesnotexist_.exe";

    internal static string resoDir = string.Empty;

    internal static AssemblyLoadContext alc = null!;

    static void Main(string[] args)
    {
        var silentExceptionLog = $"BepisLoader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
        try
        {
            resoDir = Directory.GetCurrentDirectory();

            alc = new BepisLoadContext();

            var bepinPath = Path.Combine(resoDir, "BepInEx");
            var bepinArg = Array.IndexOf(args.Select(x => x?.ToLowerInvariant()).ToArray(), "--bepinex-target");
            if (bepinArg != -1 && args.Length > bepinArg + 1)
            {
                bepinPath = args[bepinArg + 1];
            }

            ResolveDirectories.Add(Path.Combine(bepinPath, "core"));
            AppDomain.CurrentDomain.AssemblyResolve += RemoteResolve;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveGameDll;

            var resoDllPath = Path.Combine(resoDir, "Renderite.Host.dll");
            if (!File.Exists(resoDllPath)) resoDllPath = Path.Combine(resoDir, "Resonite.dll");

            // Call BepInEx preloader
            Console.WriteLine($"[BepisLoader] Initializing BepInEx");
            Console.WriteLine($"[BepisLoader] BepInEx path: {bepinPath}");
            Console.WriteLine($"[BepisLoader] Resolve directories: {string.Join(", ", ResolveDirectories)}");

            NetCorePreloaderRunner.OuterMain(resoDllPath, bepinPath, alc);

            // Find and load Resonite
            Assembly resoAsm = null;// AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Renderite.Host" || x.GetName().Name == "Resonite");
            if (resoAsm == null)
            {
                resoAsm = alc.LoadFromAssemblyPath(resoDllPath);
            }

            var result = resoAsm.EntryPoint!.Invoke(null, [args]);
            if (result is Task task) task.Wait();
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


    static Assembly? RemoteResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);

        foreach (var directory in ResolveDirectories)
        {
            if (!Directory.Exists(directory))
                continue;

            var potentialDirectories = new List<string> { directory };
            potentialDirectories.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));

            var potentialFiles = potentialDirectories.Select(x => Path.Combine(x, $"{assemblyName.Name}.dll"))
                                                     .Concat(potentialDirectories.Select(x =>
                                                                 Path.Combine(x, $"{assemblyName.Name}.exe")));

            foreach (var path in potentialFiles)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    if (assembly.GetName().Name == assemblyName.Name)
                        return assembly;
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        return null;
    }

    static Assembly? ResolveGameDll(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        return ResolveInternal(assemblyName);
    }

    static Assembly? ResolveInternal(AssemblyName assemblyName)
    {
        var found = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName.Name);
        if (found != null)
        {
            return found;
        }

        if (assemblyName.Name == "System.Management") return null;

        var targetPath = Path.Combine(resoDir, assemblyName.Name + ".dll");
        if (File.Exists(targetPath))
        {
            var asm = alc.LoadFromAssemblyPath(targetPath);
            return asm;
        }

        return null;
    }

    private class BepisLoadContext : AssemblyLoadContext
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return ResolveInternal(assemblyName);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var rid = GetRuntimeIdentifier();

            var nativeLibs = Path.Join(resoDir, "runtimes", rid, "native");
            IEnumerable<string> potentialPaths = [unmanagedDllName, Path.Combine(nativeLibs, GetUnmanagedLibraryName(unmanagedDllName))];
            if (unmanagedDllName.EndsWith("steam_api64.so")) potentialPaths = ((IEnumerable<string>)["libsteam_api.so"]).Concat(potentialPaths);

            foreach (var path in potentialPaths)
            {
                if (File.Exists(path))
                {
                    var dll = LoadUnmanagedDllFromPath(path);
                    if (dll != IntPtr.Zero)
                    {
                        return dll;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static string GetRuntimeIdentifier()
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                os = "win";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                os = "osx";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                os = "linux";
            else
                throw new PlatformNotSupportedException();

            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X86 => "-x86",
                Architecture.X64 => "-x64",
                Architecture.Arm64 => "-arm64",
                _ => ""
            };

            return $"{os}{arch}";
        }
        private static string GetUnmanagedLibraryName(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"{name}.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return $"lib{name}.so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"lib{name}.dylib";

            throw new PlatformNotSupportedException();
        }

    }

}

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

    internal static void OuterMain(string filename, string bepinRootPath, AssemblyLoadContext alc)
    {
        PlatformUtils.SetPlatform();

        Paths.SetExecutablePath(filename, bepinRootPath);

        AppDomain.CurrentDomain.AssemblyResolve += SharedEntrypoint.LocalResolve;

        Utility.LoadContext = alc ?? AssemblyLoadContext.Default;

        PreloaderMain();
    }
}
