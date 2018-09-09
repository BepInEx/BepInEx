using System;
using Harmony;

namespace BepInEx.Bootstrap
{
    internal static class UnityPatches
    {
        public static void Apply()
        {
            HarmonyInstance.Create("com.bepinex.unitypatches").PatchAll(typeof(UnityPatches));
        }

#if UNITY_2018
        /*
         * DESC: Workaround for Trace class not working because of missing .config file
         * AFFECTS: Unity 2018+
         */
        [HarmonyPatch(typeof(AppDomain))]
        [HarmonyPatch(nameof(AppDomain.SetupInformation), PropertyMethod.Getter)]
        [HarmonyPostfix]
        public static void GetExeConfigName(AppDomainSetup __result)
        {
            __result.ApplicationBase = $"file://{Paths.GameRootPath}";
            __result.ConfigurationFile = "app.config";
        }
#endif
    }
}