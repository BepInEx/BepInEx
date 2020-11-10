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
			Harmony.CreateAndPatchAll(typeof(HarmonyInteropFix), "org.bepinex.fixes.harmonyinterop");
		}

		[HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFile), typeof(string))]
		[HarmonyPrefix]
		private static void OnAssemblyLoad(string path)
		{
			HarmonyInterop.TryShim(path, Logger.LogWarning, TypeLoader.ReaderParameters);
		}
	}
}