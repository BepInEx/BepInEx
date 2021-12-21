using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.RuntimeFixes;

namespace BepInEx.Preloader.Unity;

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
        "Mono.Cecil.Rocks.dll"
    };

    private static void LoadCriticalAssemblies()
    {
        foreach (var criticalAssembly in CriticalAssemblies)
            try
            {
                Assembly.LoadFile(Path.Combine(Paths.BepInExAssemblyDirectory, criticalAssembly));
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

        Paths.SetExecutablePath(EnvVars.DOORSTOP_PROCESS_PATH, bepinPath,
                                EnvVars.DOORSTOP_MANAGED_FOLDER_DIR,
                                EnvVars.DOORSTOP_DLL_SEARCH_DIRS);

        LoadCriticalAssemblies();
        AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;
        // Remove temporary resolver early so it won't override local resolver
        AppDomain.CurrentDomain.AssemblyResolve -= DoorstopEntrypoint.ResolveCurrentDirectory;
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

        if (Utility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
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
    public static void Main()
    {
        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        var silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

        try
        {
            EnvVars.LoadVars();

            var gamePath = Path.GetDirectoryName(EnvVars.DOORSTOP_PROCESS_PATH) ?? ".";
            silentExceptionLog = Path.Combine(gamePath, silentExceptionLog);

            // Get the path of this DLL via Doorstop env var because Assembly.Location mangles non-ASCII characters on some versions of Mono for unknown reasons
            preloaderPath = Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH));

            AppDomain.CurrentDomain.AssemblyResolve += ResolveCurrentDirectory;

            // In some versions of Unity 4, Mono tries to resolve BepInEx.dll prematurely because of the call to Paths.SetExecutablePath
            // To prevent that, we have to use reflection and a separate startup class so that we can install required assembly resolvers before the main code
            typeof(DoorstopEntrypoint).Assembly.GetType($"BepInEx.Preloader.Unity.{nameof(UnityPreloaderRunner)}")
                                      ?.GetMethod(nameof(UnityPreloaderRunner.PreloaderPreMain))
                                      ?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveCurrentDirectory;
        }
    }

    internal static Assembly ResolveCurrentDirectory(object sender, ResolveEventArgs args)
    {
        // Can't use Utils here because it's not yet resolved
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
