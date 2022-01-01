using System;
using System.ComponentModel;
using BepInEx.Configuration;

namespace BepInEx.Preloader.RuntimeFixes;

public static class HarmonyBackendFix
{
    private static readonly ConfigEntry<MonoModBackend> ConfigHarmonyBackend = ConfigFile.CoreConfig.Bind(
     "Preloader",
     "HarmonyBackend",
     MonoModBackend.auto,
     "Specifies which MonoMod backend to use for Harmony patches. Auto uses the best available backend.\nThis setting should only be used for development purposes (e.g. debugging in dnSpy). Other code might override this setting.");

    public static void Initialize()
    {
        switch (ConfigHarmonyBackend.Value)
        {
            case MonoModBackend.auto:
                break;
            case MonoModBackend.dynamicmethod:
            case MonoModBackend.methodbuilder:
            case MonoModBackend.cecil:
                Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", ConfigHarmonyBackend.Value.ToString());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ConfigHarmonyBackend), ConfigHarmonyBackend.Value,
                                                      "Unknown backend");
        }
    }

    private enum MonoModBackend
    {
        // Enum names are important!
        [Description("Auto")]
        auto = 0,

        [Description("DynamicMethod")]
        dynamicmethod,

        [Description("MethodBuilder")]
        methodbuilder,

        [Description("Cecil")]
        cecil
    }
}
