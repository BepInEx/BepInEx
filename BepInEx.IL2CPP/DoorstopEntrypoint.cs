using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Preloader.Core;

namespace BepInEx.IL2CPP;

internal static class UnityPreloaderRunner
{
    public static void PreloaderMain(string[] args)
    {
        var bepinPath =
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH)));

        PlatformUtils.SetPlatform();

        Paths.SetExecutablePath(EnvVars.DOORSTOP_PROCESS_PATH, bepinPath, EnvVars.DOORSTOP_MANAGED_FOLDER_DIR,
                                EnvVars.DOORSTOP_DLL_SEARCH_DIRS);

        // Cecil 0.11 requires one to manually set up list of trusted assemblies for assembly resolving
        AppDomain.CurrentDomain.AddCecilPlatformAssemblies(Paths.ManagedPath);

        Preloader.IL2CPPUnhollowedPath = Path.Combine(Paths.BepInExRootPath, "unhollowed");

        AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;
        AppDomain.CurrentDomain.AssemblyResolve -= DoorstopEntrypoint.ResolveCurrentDirectory;


        Preloader.Run();
    }

    internal static Assembly LocalResolve(object sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);

        var foundAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                     .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

        if (foundAssembly != null)
            return foundAssembly;

        if (Utility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Preloader.IL2CPPUnhollowedPath, out foundAssembly))
            return foundAssembly;

        return null;
    }
}

internal static class DoorstopEntrypoint
{
    private static string preloaderPath;

    /// <summary>
    ///     The main entrypoint of BepInEx, called from Doorstop.
    /// </summary>
    /// <param name="args">
    ///     The arguments passed in from Doorstop. First argument is the path of the currently executing
    ///     process.
    /// </param>
    public static void Main(string[] args)
    {
        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        var silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
        Mutex mutex = null;

        try
        {
            EnvVars.LoadVars();

            silentExceptionLog =
                Path.Combine(Path.GetDirectoryName(EnvVars.DOORSTOP_PROCESS_PATH), silentExceptionLog);

            // Get the path of this DLL via Doorstop env var because Assembly.Location mangles non-ASCII characters on some versions of Mono for unknown reasons
            preloaderPath = Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH));

            mutex = new Mutex(false,
                              Process.GetCurrentProcess().ProcessName + EnvVars.DOORSTOP_PROCESS_PATH +
                              typeof(DoorstopEntrypoint).FullName);
            mutex.WaitOne();

            AppDomain.CurrentDomain.AssemblyResolve += ResolveCurrentDirectory;

            UnityPreloaderRunner.PreloaderMain(args);
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());
        }
        finally
        {
            mutex?.ReleaseMutex();
        }
    }

    public static Assembly ResolveCurrentDirectory(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name);

        try
        {
            return Assembly.LoadFile(Path.Combine(preloaderPath, $"{name.Name}.dll"));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
