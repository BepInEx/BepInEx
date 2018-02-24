using BepInEx;
using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ResourceRedirector
{
    public class ResourceRedirector : BaseUnityPlugin
    {
        public override string ID => "com.bepis.bepinex.resourceredirector";
        public override string Name => "Asset Emulator";
        public override Version Version => new Version("1.3");

        public static string EmulatedDir => Path.Combine(Utility.ExecutingDirectory, "abdata-emulated");

        public static bool EmulationEnabled;



        public delegate bool AssetHandler(string assetBundleName, string assetName, Type type, string manifestAssetBundleName, out AssetBundleLoadAssetOperation result);

        public static List<AssetHandler> AssetResolvers = new List<AssetHandler>();



        void Awake()
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

                    BepInLogger.Log($"Loading emulated asset {path}");

                    return new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadTexture(path));
                }
                else if (type == typeof(AudioClip))
                {
                    string path = Path.Combine(dir, $"{assetName}.wav");

                    if (!File.Exists(path))
                        return __result;

                    BepInLogger.Log($"Loading emulated asset {path}");

                    return new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadAudioClip(path, AudioType.WAV));
                }
            }

            //otherwise return normal asset
            return __result;
        }
    }
}
