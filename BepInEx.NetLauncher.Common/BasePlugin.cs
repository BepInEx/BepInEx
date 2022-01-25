using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.NetLauncher.Common
{
    public abstract class BasePlugin
    {
        protected BasePlugin()
        {
            var metadata = MetadataHelper.GetMetadata(this);

            HarmonyInstance = new Harmony("BepInEx.Plugin." + metadata.GUID);

            Log = Logger.CreateLogSource(metadata.Name);

            Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
        }

        public ManualLogSource Log { get; }

        public ConfigFile Config { get; }

        public Harmony HarmonyInstance { get; set; }

        public abstract void Load();

        public virtual bool Unload() => false;
    }
}
