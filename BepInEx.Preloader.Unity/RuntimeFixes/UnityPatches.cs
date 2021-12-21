using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace BepInEx.Preloader.RuntimeFixes;

internal static class UnityPatches
{
    private static Harmony HarmonyInstance { get; set; }

    public static Dictionary<string, string> AssemblyLocations { get; } =
        new(StringComparer.InvariantCultureIgnoreCase);

    public static void Apply()
    {
        HarmonyInstance = Harmony.CreateAndPatchAll(typeof(UnityPatches));

        try
        {
            TraceFix.ApplyFix();
        }
        catch { } //ignore everything, if it's thrown an exception, we're using an assembly that has already fixed this
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.Location), MethodType.Getter)]
    public static void GetLocation(ref string __result, Assembly __instance)
    {
        if (AssemblyLocations.TryGetValue(__instance.FullName, out var location))
            __result = location;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.CodeBase), MethodType.Getter)]
    public static void GetCodeBase(ref string __result, Assembly __instance)
    {
        if (AssemblyLocations.TryGetValue(__instance.FullName, out var location))
            __result = $"file://{location.Replace('\\', '/')}";
    }
}
