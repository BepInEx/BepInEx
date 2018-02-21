using BepInEx;
using Illusion.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ResourceRedirector
{
    static class BGMLoader
    {
        public static bool HandleAsset(string assetBundleName, string assetName, Type type, string manifestAssetBundleName, out AssetBundleLoadAssetOperation result)
        {
            if (assetName.StartsWith("bgm"))
            {
                string path;

                switch ((BGM)int.Parse(assetName.Remove(0, 4)))
                {
                    case BGM.Title:
                    default:
                        path = $"{BepInEx.Common.Utility.PluginsDirectory}\\title.wav";
                        break;
                    case BGM.Custom:
                        path = $"{BepInEx.Common.Utility.PluginsDirectory}\\custom.wav";
                        break;
                }

                if (File.Exists(path))
                {

                    Chainloader.Log($"Loading {path}");

                    result = new AssetBundleLoadAssetOperationSimulation(AssetLoader.LoadAudioClip(path, AudioType.WAV));

                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
