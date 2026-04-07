using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace ValheimArm64Smoke.Harmony
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "dev.bepinex.valheimarm64.harmony";
        private const string PluginName = "Valheim ARM64 Smoke Test (Harmony)";
        private const string PluginVersion = "0.1.0";

        private static readonly string[] TargetCandidates =
        {
            "FejdStartup:Awake",
            "FejdStartup:Start",
            "Game:Awake",
            "ZNet:Awake"
        };

        private static BepInEx.Logging.ManualLogSource log;
        private static string markerPath;
        private HarmonyLib.Harmony harmony;
        private static bool prefixLogged;

        private void Awake()
        {
            log = Logger;
            markerPath = Path.Combine(Paths.BepInExRootPath, "valheim-arm64-harmony.txt");

            try
            {
                var target = ResolveTargetMethod();
                if (target == null)
                {
                    WriteMarker("status=target_not_found");
                    Logger.LogWarning($"{PluginName} could not resolve a Valheim target method");
                    return;
                }

                harmony = new HarmonyLib.Harmony(PluginGuid);
                var prefix = typeof(Plugin).GetMethod(nameof(TargetPrefix), BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                WriteMarker(
                    "status=patched",
                    $"target={target.DeclaringType.FullName}.{target.Name}",
                    $"module={target.Module.Name}");
                Logger.LogInfo($"{PluginName} patched {target.DeclaringType.FullName}.{target.Name}");
            }
            catch (Exception ex)
            {
                WriteMarker("status=patch_failed", ex.ToString());
                Logger.LogError($"{PluginName} failed while patching: {ex}");
            }
        }

        private static MethodBase ResolveTargetMethod()
        {
            foreach (var candidate in TargetCandidates)
            {
                var splitIndex = candidate.IndexOf(':');
                if (splitIndex <= 0 || splitIndex >= candidate.Length - 1)
                    continue;

                var typeName = candidate.Substring(0, splitIndex);
                var methodName = candidate.Substring(splitIndex + 1);
                var type = AccessTools.TypeByName(typeName);
                var method = type != null ? AccessTools.DeclaredMethod(type, methodName) : null;

                if (method != null)
                    return method;
            }

            return null;
        }

        private static void TargetPrefix(MethodBase __originalMethod)
        {
            if (prefixLogged)
                return;

            prefixLogged = true;
            var originalName = $"{__originalMethod.DeclaringType.FullName}.{__originalMethod.Name}";
            WriteMarker("status=prefix_hit", $"method={originalName}", $"timestamp_utc={DateTime.UtcNow:O}");
            log?.LogInfo($"{PluginName} prefix executed for {originalName}");
        }

        private static void WriteMarker(params string[] lines)
        {
            try
            {
                File.WriteAllLines(markerPath, lines);
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Could not write smoke marker {markerPath}: {ex}");
            }
        }
    }
}
