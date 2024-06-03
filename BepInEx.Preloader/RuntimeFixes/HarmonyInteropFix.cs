using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyXInterop;

namespace BepInEx.Preloader.RuntimeFixes
{
	internal static class HarmonyInteropFix
	{
		public static void Apply()
		{
			HarmonyInterop.Initialize(Paths.CachePath);
			HarmonyLib.Harmony.CreateAndPatchAll(typeof(HarmonyInteropFix), "org.bepinex.fixes.harmonyinterop");
		}

		[HarmonyReversePatch]
		[HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFile), typeof(string))]
		private static Assembly LoadFile(string path) => null;

		[HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFile), typeof(string))]
		[HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFrom), typeof(string))]
		[HarmonyPrefix]
		private static bool OnAssemblyLoad(ref Assembly __result, string __0)
		{
			HarmonyInterop.TryShim(__0, Paths.GameRootPath, Logger.LogWarning, TypeLoader.ReaderParameters);
			__result = LoadFile(__0);
			return true;
		}
	}
}