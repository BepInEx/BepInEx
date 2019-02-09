using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Harmony;
using Harmony;

namespace BepInEx.Preloader.RuntimeFixes
{
	internal static class UnityPatches
	{
		public static HarmonyInstance HarmonyInstance { get; } = HarmonyInstance.Create("com.bepinex.unitypatches");

		public static Dictionary<string, string> AssemblyLocations { get; } =
			new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

		public static void Apply()
		{
			HarmonyWrapper.PatchAll(typeof(UnityPatches), HarmonyInstance);

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
			if (AssemblyLocations.TryGetValue(__instance.FullName, out string location))
				__result = location;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Assembly), nameof(Assembly.CodeBase), MethodType.Getter)]
		public static void GetCodeBase(ref string __result, Assembly __instance)
		{
			if (AssemblyLocations.TryGetValue(__instance.FullName, out string location))
				__result = $"file://{location.Replace('\\', '/')}";
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