using System;
using System.IO;
using BepInEx.Preloader.Core;
using BepInEx.Unity.Mono.Preloader;

// ReSharper disable once CheckNamespace
namespace Doorstop;

internal static class Entrypoint
{
    /// <summary>
    ///     The main entrypoint of BepInEx, called from Doorstop.
    /// </summary>
    public static void Start()
    {
        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        var silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

        try
        {
            EnvVars.LoadVars();

            var gamePath = Path.GetDirectoryName(EnvVars.DOORSTOP_PROCESS_PATH) ?? ".";
            silentExceptionLog = Path.Combine(gamePath, silentExceptionLog);

            // In some versions of Unity 4, Mono tries to resolve BepInEx.dll prematurely because of the call to Paths.SetExecutablePath
            // To prevent that, we have to use reflection and a separate startup class so that we can install required assembly resolvers before the main code
            typeof(Entrypoint).Assembly.GetType($"BepInEx.Unity.Mono.Preloader.{nameof(UnityPreloaderRunner)}")
                              ?.GetMethod(nameof(UnityPreloaderRunner.PreloaderPreMain))
                              ?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());
        }
    }
}
