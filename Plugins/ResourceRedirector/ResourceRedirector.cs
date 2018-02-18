using BepInEx;
using Harmony;
using Illusion.Game;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResourceRedirector
{
    public class ResourceRedirector : BaseUnityPlugin
    {
        public override string Name => "Resource Redirector";

        public ResourceRedirector()
        {
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.resourceredirector");



            MethodInfo original = AccessTools.Method(typeof(AssetBundleManager), "LoadAsset", new[] { typeof(string), typeof(string), typeof(Type), typeof(string) });

            HarmonyMethod postfix = new HarmonyMethod(typeof(ResourceRedirector).GetMethod("LoadAssetPostHook"));

            harmony.Patch(original, null, postfix);




            original = AccessTools.Method(typeof(AssetBundleManager), "LoadAllAsset", new[] { typeof(string), typeof(Type), typeof(string) });

            postfix = new HarmonyMethod(typeof(ResourceRedirector).GetMethod("LoadAllAssetPostHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(Manager.Sound), "Bind");

            var prefix = new HarmonyMethod(typeof(ResourceRedirector).GetMethod("BindPreHook"));

            harmony.Patch(original, prefix, null);
        }

        public static void LoadAssetPostHook(string assetBundleName, string assetName, Type type, string manifestAssetBundleName)
        {
            //Console.WriteLine($"{assetBundleName} : {assetName} : {type.FullName} : {manifestAssetBundleName ?? ""}");
        }

        public static void LoadAllAssetPostHook(string assetBundleName, Type type, string manifestAssetBundleName = null)
        {
            //Console.WriteLine($"{assetBundleName} : {type.FullName} : {manifestAssetBundleName ?? ""}");
        }

        public static FieldInfo f_typeObjects = typeof(Manager.Sound).GetField("typeObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo f_settingObjects = typeof(Manager.Sound).GetField("settingObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        public static PropertyInfo f_clip = typeof(LoadAudioBase).GetProperty("clip", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        

        public static void BindPreHook(ref LoadSound script)
        {
            if (script.audioSource == null)
            {
                int type = (int)script.type;

                Transform[] typeObjects = (Transform[])f_typeObjects.GetValue(Manager.Game.Instance);
                GameObject[] settingObjects = (GameObject[])f_settingObjects.GetValue(Manager.Game.Instance);

                Manager.Sound.Instance.SetParent(typeObjects[type], script, settingObjects[type]);
            }

            if (script.clip.name.StartsWith("bgm"))
            {
                string path;

                switch ((BGM)int.Parse(script.clip.name.Remove(0, 4)))
                {
                    case BGM.Title:
                    default:
                        path = "file://" + BepInEx.Common.Utility.PluginsDirectory.Replace('\\', '/') + "/title.wav";
                        break;
                    case BGM.Custom:
                        path = "file://" + BepInEx.Common.Utility.PluginsDirectory.Replace('\\', '/') + "/custom.wav";
                        break;
                }

                
                Console.WriteLine($"Loaded {path}");
                WWW loadGachi = new WWW(path);

                
                AudioClip clip = loadGachi.GetAudioClip(false);

                //force single threaded loading instead of using a coroutine
                while (!clip.isReadyToPlay) { }


                script.audioSource.clip = clip;

                f_clip.SetValue(script, clip, null);
                
                loadGachi.Dispose();
            }
        }
    }
}
