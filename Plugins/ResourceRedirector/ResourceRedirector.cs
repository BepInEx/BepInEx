using BepInEx;
using BepInEx.Common;
using Harmony;
using Illusion.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResourceRedirector
{
    public class ResourceRedirector : BaseUnityPlugin
    {
        public override string Name => "Asset Emulator";

        public static string EmulatedDir => Path.Combine(Utility.ExecutingDirectory, "abdata-emulated");

        public static bool EmulationEnabled;



        public delegate bool AssetHandler(string assetBundleName, string assetName, Type type, string manifestAssetBundleName, out AssetBundleLoadAssetOperation result);

        public static List<AssetHandler> AssetResolvers = new List<AssetHandler>();



        public ResourceRedirector()
        {
            Hooks.InstallHooks();

            EmulationEnabled = Directory.Exists(EmulatedDir);

            AssetResolvers.Add(BGMLoader.HandleAsset);
        }

        
        public static AssetBundleLoadAssetOperation HandleAsset(string assetBundleName, string assetName, Type type, string manifestAssetBundleName, ref AssetBundleLoadAssetOperation __result)
        {
            foreach (var handler in AssetResolvers)
            {
                if (handler.Invoke(assetBundleName, assetName, type, manifestAssetBundleName, out AssetBundleLoadAssetOperation result))
                    return result;
            }

            //emulate asset load
            string dir = Path.Combine(EmulatedDir, assetBundleName.Replace('/', '\\').Replace(".unity3d", ""));

            if (Directory.Exists(dir))
            {
                if (type == typeof(Texture2D))
                {
                    string path = Path.Combine(dir, $"{assetName}.png");

                    if (!File.Exists(path))
                        return __result;

                    Chainloader.Log($"Loading emulated asset {path}");

                    return new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadTexture(path));
                }
                else if (type == typeof(AudioClip))
                {
                    string path = Path.Combine(dir, $"{assetName}.wav");

                    if (!File.Exists(path))
                        return __result;

                    Chainloader.Log($"Loading emulated asset {path}");

                    return new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadAudioClip(path, AudioType.WAV));
                }
                else if (type == typeof(TextAsset))
                {
                    string path = Path.Combine(dir, $"{assetName}.bytes");

                    if (!File.Exists(path))
                        return __result;

                    Chainloader.Log($"Loading emulated asset {path}");

                    return new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadTextAsset(path));
                }
            }

            //otherwise return normal asset
            return __result;
        }
    }
}
