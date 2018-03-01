using BepInEx;
using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ResourceRedirector
{
    static class Hooks
    {
        public static void InstallHooks()
        {
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.resourceredirector");


            MethodInfo original = AccessTools.Method(typeof(AssetBundleManager), "LoadAsset", new[] { typeof(string), typeof(string), typeof(Type), typeof(string) });

            HarmonyMethod postfix = new HarmonyMethod(typeof(Hooks).GetMethod("LoadAssetPostHook"));

            harmony.Patch(original, null, postfix);


            original = AccessTools.Method(typeof(AssetBundleManager), "LoadAssetBundle", new[] { typeof(string), typeof(bool), typeof(string) });

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("LoadAssetBundlePostHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(AssetBundleManager), "LoadAssetAsync", new[] { typeof(string), typeof(string), typeof(Type), typeof(string) });

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("LoadAssetAsyncPostHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(AssetBundleManager), "LoadAllAsset", new[] { typeof(string), typeof(Type), typeof(string) });

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("LoadAllAssetPostHook"));

            harmony.Patch(original, null, postfix);
        }

        public static void LoadAssetPostHook(ref AssetBundleLoadAssetOperation __result, string assetBundleName, string assetName, Type type, string manifestAssetBundleName)
        {
            try
            {
                //BepInLogger.Log($"{assetBundleName} : {assetName} : {type.FullName} : {manifestAssetBundleName ?? ""}");

                __result = ResourceRedirector.HandleAsset(assetBundleName, assetName, type, manifestAssetBundleName, ref __result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("---" + nameof(LoadAssetPostHook) + "---");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("---");
            }
        }

        public static void LoadAssetBundlePostHook(string assetBundleName, bool isAsync, string manifestAssetBundleName)
        {
            try
            {
                //BepInLogger.Log($"{assetBundleName} : {manifestAssetBundleName} : {isAsync}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("---" + nameof(LoadAssetBundlePostHook) + "---");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("---");
            }
        }

        public static void LoadAssetAsyncPostHook(ref AssetBundleLoadAssetOperation __result, string assetBundleName, string assetName, Type type, string manifestAssetBundleName)
        {
            try
            {
                //BepInLogger.Log($"{assetBundleName} : {assetName} : {type.FullName} : {manifestAssetBundleName ?? ""}", true);

                __result = ResourceRedirector.HandleAsset(assetBundleName, assetName, type, manifestAssetBundleName, ref __result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("---" + nameof(LoadAssetAsyncPostHook) + "---");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("---");
            }
        }

        public static void LoadAllAssetPostHook(ref AssetBundleLoadAssetOperation __result, string assetBundleName, Type type, string manifestAssetBundleName = null)
        {
            try
            {
                //BepInLogger.Log($"{assetBundleName} : {type.FullName} : {manifestAssetBundleName ?? ""}");

                if (assetBundleName == "sound/data/systemse/brandcall/00.unity3d" ||
                    assetBundleName == "sound/data/systemse/titlecall/00.unity3d")
                {
                    string dir = $"{BepInEx.Common.Utility.PluginsDirectory}\\introclips";

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var files = Directory.GetFiles(dir, "*.wav");

                    if (files.Length == 0)
                        return;

                    List<UnityEngine.Object> loadedClips = new List<UnityEngine.Object>();

                    foreach (string path in files)
                        loadedClips.Add(AssetLoader.LoadAudioClip(path, AudioType.WAV));

                    __result = new AssetBundleLoadAssetOperationSimulation(loadedClips.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("---" + nameof(LoadAllAssetPostHook) + "---");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("---");
            }
        }
    }
}
