using System;
using BepInEx.Harmony;
using Harmony;

namespace BepInEx.Bootstrap
{
    internal static class UnityPatches
	{
		public static HarmonyInstance HarmonyInstance { get; } = HarmonyInstance.Create("com.bepinex.unitypatches");

		public static void Apply()
        {
            HarmonyWrapper.PatchAll(typeof(UnityPatches), HarmonyInstance);
        }

#if UNITY_2018
        /*
         * DESC: Workaround for Trace class not working because of missing .config file
         * AFFECTS: Unity 2018+
         */
        [HarmonyPostfix, HarmonyPatch(typeof(AppDomain), nameof(AppDomain.SetupInformation), MethodType.Getter)]
        public static void GetExeConfigName(AppDomainSetup __result)
        {
            __result.ApplicationBase = $"file://{Paths.GameRootPath}";
            __result.ConfigurationFile = "app.config";
        }
#endif
    }
}