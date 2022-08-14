using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Preloader.Core;

namespace BepInEx.Unity.IL2CPP;

internal static class UnityPreloaderRunner
{
    public static void PreloaderMain()
    {
        var bepinPath =
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH)));

        PlatformUtils.SetPlatform();

        Paths.SetExecutablePath(EnvVars.DOORSTOP_PROCESS_PATH, bepinPath, EnvVars.DOORSTOP_MANAGED_FOLDER_DIR, false,
                                EnvVars.DOORSTOP_DLL_SEARCH_DIRS);

        // Cecil 0.11 requires one to manually set up list of trusted assemblies for assembly resolving
        // The main BCL path
        AppDomain.CurrentDomain.AddCecilPlatformAssemblies(Paths.ManagedPath);
        // The parent path -> .NET has some extra managed DLLs in there
        AppDomain.CurrentDomain.AddCecilPlatformAssemblies(Path.GetDirectoryName(Paths.ManagedPath));

        AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

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
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
            return foundAssembly;

        return null;
    }
}
