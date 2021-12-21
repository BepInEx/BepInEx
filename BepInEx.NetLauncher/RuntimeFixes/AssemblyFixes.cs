using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.NetLauncher.RuntimeFixes;

internal class AssemblyFixes
{
    private static Assembly EntryAssembly { get; set; }

    public static Dictionary<string, string> AssemblyLocations { get; } =
        new(StringComparer.InvariantCultureIgnoreCase);

    public static void Execute(Assembly entryAssembly)
    {
        EntryAssembly = entryAssembly;
        Harmony.CreateAndPatchAll(typeof(AssemblyFixes), "io.bepinex.assemblyfix");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.GetEntryAssembly))]
    public static bool GetEntryAssemblyPrefix(ref Assembly __result)
    {
        Logger.Log(LogLevel.Debug, "Ran GetEntryAssembly");

        __result = EntryAssembly;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.Load), typeof(string))]
    public static bool LoadByName(ref Assembly __result, string assemblyString)
    {
        __result = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(x => x.GetName().FullName == assemblyString);


        Logger.Log(LogLevel.Debug, $"LoadByName: {assemblyString} : {__result != null}");

        return __result != null;
    }

    [HarmonyPrefix]
    [HarmonyPatch("System.Reflection.RuntimeAssembly", nameof(Assembly.Location), MethodType.Getter)]
    public static bool GetLocation(ref string __result, Assembly __instance)
    {
        Logger.Log(LogLevel.Debug, $"GetLocation: {__instance.FullName}");

        if (AssemblyLocations.TryGetValue(__instance.FullName, out var location))
        {
            __result = location;
            return false;
        }

        return true;
    }

    // This is commented out because it should be implemented alongside GetLocation, but seems to crash the runtime
    // Might be because the target type should be System.Reflection.RuntimeAssembly

    //[HarmonyPrefix]
    //[HarmonyPatch(typeof(Assembly), nameof(Assembly.CodeBase), MethodType.Getter)]
    //public static bool GetCodeBase(ref string __result, Assembly __instance)
    //{
    //    if (AssemblyLocations.TryGetValue(__instance.FullName, out var location))
    //    {
    //        __result = $"file://{location.Replace('\\', '/')}";
    //        return false;
    //    }

    //    return true;
    //}
}
