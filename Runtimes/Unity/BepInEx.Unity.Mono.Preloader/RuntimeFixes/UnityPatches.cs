using System.IO;
using System.Reflection;
using BepInEx.Unity.Mono.Preloader.Utils;
using HarmonyLib;

namespace BepInEx.Unity.Mono.Preloader.RuntimeFixes;

internal static class UnityPatches
{
    private static Harmony HarmonyInstance { get; set; }

    public static void Apply()
    {
        HarmonyInstance = Harmony.CreateAndPatchAll(typeof(UnityPatches));

        try
        {
            TraceFix.ApplyFix();
        }
        catch { } //ignore everything, if it's thrown an exception, we're using an assembly that has already fixed this
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFile), typeof(string))]
    [HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFrom), typeof(string))]
    public static bool LoadFilePrefix(string __0, ref Assembly __result)
    {
        if (!File.Exists(__0))
            throw new FileNotFoundException(__0);
        __result = MonoAssemblyHelper.Load(__0);
        return false;
    }
}
