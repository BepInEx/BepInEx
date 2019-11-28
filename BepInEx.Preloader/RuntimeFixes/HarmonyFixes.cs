using System;
using System.Diagnostics;
using HarmonyLib;

namespace BepInEx.Preloader.RuntimeFixes
{
	internal static class HarmonyFixes
	{
		public static void Apply()
		{
			try
			{
				var harmony = new HarmonyLib.Harmony("BepInEx.Preloader.RuntimeFixes.HarmonyFixes");
				harmony.Patch(AccessTools.Method(typeof(Traverse), nameof(Traverse.GetValue), new Type[0]), null, new HarmonyMethod(typeof(HarmonyFixes), nameof(GetValue)));
			}
			catch (Exception e)
			{
				Logging.Logger.LogError(e);
			}
		}

		private static void GetValue(Traverse __instance)
		{
			if (!__instance.FieldExists() && !__instance.MethodExists() && !__instance.TypeExists())
				Logging.Logger.LogWarning("Traverse.GetValue was called while not pointing at an existing Field, Property, Method or Type. The return value can be unexpected.\n" + new StackTrace());
		}
	}
}