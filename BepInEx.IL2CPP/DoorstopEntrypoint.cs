using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Preloader.Core;

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
        Mutex mutex = null;

        try
        {
            EnvVars.LoadVars();

            silentExceptionLog =
                Path.Combine(Path.GetDirectoryName(EnvVars.DOORSTOP_PROCESS_PATH), silentExceptionLog);

            var mutexId = Utility.HashStrings(Process.GetCurrentProcess().ProcessName, EnvVars.DOORSTOP_PROCESS_PATH,
                                              typeof(Entrypoint).FullName);

            mutex = new Mutex(false, $"Global\\{mutexId}");
            mutex.WaitOne();

            UnityPreloaderRunner.PreloaderMain();
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
}
