using System;
using System.IO;

namespace BepInEx.Preloader.Core;

/// <summary>
///     Doorstop environment variables, passed into the BepInEx preloader.
///     <para>https://github.com/NeighTools/UnityDoorstop/wiki#environment-variables</para>
/// </summary>
public static class EnvVars
{
    /// <summary>
    ///     Path to the assembly that was invoked via Doorstop. Contains the same value as in "targetAssembly" configuration
    ///     option in the config file.
    /// </summary>
    public static string DOORSTOP_INVOKE_DLL_PATH { get; private set; }

    /// <summary>
    ///     Full path to the game's "Managed" folder that contains all the game's managed assemblies
    /// </summary>
    public static string DOORSTOP_MANAGED_FOLDER_DIR { get; private set; }

    /// <summary>
    ///     Full path to the game executable currently running.
    /// </summary>
    public static string DOORSTOP_PROCESS_PATH { get; private set; }

    /// <summary>
    ///     Array of paths where Mono searches DLLs from before assembly resolvers are invoked.
    /// </summary>
    public static string[] DOORSTOP_DLL_SEARCH_DIRS { get; private set; }

    internal static void LoadVars()
    {
        DOORSTOP_INVOKE_DLL_PATH = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
        DOORSTOP_MANAGED_FOLDER_DIR = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
        DOORSTOP_PROCESS_PATH = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
        DOORSTOP_DLL_SEARCH_DIRS =
            Environment.GetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS")?.Split(Path.PathSeparator) ??
            new string[0];
    }
}
