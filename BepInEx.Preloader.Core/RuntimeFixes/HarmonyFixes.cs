using System;
using System.Diagnostics;
using HarmonyLib;

namespace BepInEx.Preloader.Core.RuntimeFixes
{
	public static class HarmonyFixes
	{
		public static void Apply()
		{
			try
			{
				var harmony = new HarmonyLib.Harmony("BepInEx.Preloader.RuntimeFixes.HarmonyFixes");
				harmony.Patch(AccessTools.Method(typeof(Traverse), nameof(Traverse.GetValue), new Type[0]), null, new HarmonyMethod(typeof(HarmonyFixes), nameof(GetValue)));
				harmony.Patch(AccessTools.Method(typeof(Traverse), nameof(Traverse.SetValue), new []{ typeof(object) }), null, new HarmonyMethod(typeof(HarmonyFixes), nameof(SetValue)));
            }
			catch (Exception e)
			{
				PreloaderLogger.Log.LogError(e);
			}
		}

		private static void GetValue(Traverse __instance)
		{
			if (!__instance.FieldExists() && !__instance.MethodExists() && !__instance.TypeExists())
				PreloaderLogger.Log.LogWarning("Traverse.GetValue was called while not pointing at an existing Field, Property, Method or Type. The return value can be unexpected.\n" + new StackTrace());
		}

		private static void SetValue(Traverse __instance)
		{
			// If method exists it will crash inside traverse so only need to mention the field missing
			if (!__instance.FieldExists() && !__instance.MethodExists())
				PreloaderLogger.Log.LogWarning("Traverse.SetValue was called while not pointing at an existing Field or Property. The call will have no effect.\n" + new StackTrace());
		}
	}
}