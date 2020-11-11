using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyXInterop;

namespace BepInEx.Preloader.RuntimeFixes
{
	internal static class HarmonyInteropFix
	{
		private static Dictionary<string, string> AssemblyLocations { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		
		public static void Apply()
		{
			HarmonyInterop.Initialize(Paths.CachePath);
			Harmony.CreateAndPatchAll(typeof(HarmonyInteropFix), "org.bepinex.fixes.harmonyinterop");
		}

		[HarmonyPatch(typeof(Assembly), nameof(Assembly.LoadFile), typeof(string))]
		[HarmonyPrefix]
		private static bool OnAssemblyLoad(ref Assembly __result, string path)
		{
			var bytes = HarmonyInterop.TryShim(path, Logger.LogWarning, TypeLoader.ReaderParameters);
			if (bytes == null)
				return true;
			__result = Assembly.Load(bytes);
			AssemblyLocations[__result.FullName] = Path.GetFullPath(path);
			return false;
		}
		
		[HarmonyPostfix]
		[HarmonyPatch(typeof(Assembly), nameof(Assembly.Location), MethodType.Getter)]
		public static void GetLocation(ref string __result, Assembly __instance)
		{
			if (AssemblyLocations.TryGetValue(__instance.FullName, out string location))
				__result = location;
		}
	}
}