using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace ValheimArm64Smoke.NoHarmony
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "dev.bepinex.valheimarm64.noharmony";
        private const string PluginName = "Valheim ARM64 Smoke Test (No Harmony)";
        private const string PluginVersion = "0.1.0";

        private void Awake()
        {
            var markerPath = Path.Combine(Paths.BepInExRootPath, "valheim-arm64-noharmony.txt");
            var markerLines = new[]
            {
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"unity_version={Application.unityVersion}",
                $"unity_platform={Application.platform}",
                $"pointer_size_bits={IntPtr.Size * 8}",
                $"bepinex_version={typeof(BaseUnityPlugin).Assembly.GetName().Version}"
            };

            File.WriteAllLines(markerPath, markerLines);
            Logger.LogInfo($"{PluginName} loaded successfully");
            Logger.LogInfo($"Wrote smoke marker to {markerPath}");
        }
    }
}
