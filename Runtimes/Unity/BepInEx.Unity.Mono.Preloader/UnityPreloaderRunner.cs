using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.RuntimeFixes;
using BepInEx.Unity.Mono.Preloader.RuntimeFixes;
using BepInEx.Unity.Mono.Preloader.Utils;

namespace BepInEx.Unity.Mono.Preloader;

internal static class UnityPreloaderRunner
{
    // This is a list of important assemblies in BepInEx core folder that should be force-loaded
    // Some games can ship these assemblies in Managed folder, in which case assembly resolving bypasses our LocalResolve
    // On the other hand, renaming these assemblies is not viable because 3rd party assemblies
    // that we don't build (e.g. MonoMod, Harmony, many plugins) depend on them
    // As such, we load them early so that the game uses our version instead
    // These assemblies should be known to be rarely edited and are known to be shipped as-is with Unity assets
    private static readonly string[] CriticalAssemblies =
    {
        "Mono.Cecil.dll",
        "Mono.Cecil.Mdb.dll",
        "Mono.Cecil.Pdb.dll",
        "Mono.Cecil.Rocks.dll",
        "MonoMod.Utils.dll",
        "MonoMod.RuntimeDetour.dll",
        "0Harmony.dll"
    };

    private static void LoadCriticalAssemblies()
    {
        foreach (var criticalAssembly in CriticalAssemblies)
            try
            {
                MonoAssemblyHelper.Load(Path.Combine(Paths.BepInExAssemblyDirectory, criticalAssembly));
            }
            catch (Exception)
            {
                // Suppress error for now
                // TODO: Should we crash here if load fails? Can't use logging at this point
            }
    }

    public static void PreloaderPreMain()
    {
        PlatformUtils.SetPlatform();

        var bepinPath = Utility.ParentDirectory(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH), 2);

        Paths.SetExecutablePath(EnvVars.DOORSTOP_PROCESS_PATH,
                                bepinPath,
                                EnvVars.DOORSTOP_MANAGED_FOLDER_DIR,
                                true,
                                EnvVars.DOORSTOP_DLL_SEARCH_DIRS);

        LoadCriticalAssemblies();
        AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;
        PreloaderMain();
    }

    private static void PreloaderMain()
    {
        if (UnityPreloader.ConfigApplyRuntimePatches.Value)
        {
            XTermFix.Apply();
            ConsoleSetOutFix.Apply();
        }

        UnityPreloader.Run();
    }

    private static Assembly LocalResolve(object sender, ResolveEventArgs args)
    {
        if (!Utility.TryParseAssemblyName(args.Name, out var assemblyName))
            return null;

        // Use parse assembly name on managed side because native GetName() can fail on some locales
        // if the game path has "exotic" characters

        var validAssemblies = AppDomain.CurrentDomain
                                       .GetAssemblies()
                                       .Select(a => new
                                       {
                                           assembly = a,
                                           name = Utility.TryParseAssemblyName(a.FullName, out var name)
                                                      ? name
                                                      : null
                                       })
                                       .Where(a => a.name != null && a.name.Name == assemblyName.Name)
                                       .OrderByDescending(a => a.name.Version)
                                       .ToList();

        // First try to match by version, then just pick the best match (generally highest)
        // This should mainly affect cases where the game itself loads some assembly (like Mono.Cecil) 
        var foundMatch = validAssemblies.FirstOrDefault(a => a.name.Version == assemblyName.Version) ??
                         validAssemblies.FirstOrDefault();
        var foundAssembly = foundMatch?.assembly;

        if (foundAssembly != null)
            return foundAssembly;

        if (MonoAssemblyHelper.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
         || MonoAssemblyHelper.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
         || MonoAssemblyHelper.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
            return foundAssembly;

        return null;
    }
}
