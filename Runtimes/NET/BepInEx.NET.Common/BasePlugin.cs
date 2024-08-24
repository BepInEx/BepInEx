using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Contract;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.NET.Common
{
    public abstract class BasePlugin : IPlugin
    {
        protected BasePlugin()
        {
            var metadata = MetadataHelper.GetPluginMetadata(this);

            HarmonyInstance = new Harmony("BepInEx.Plugin." + metadata.GUID);

            Info = BaseChainloader<BasePlugin>.GetPluginInfoFromGuid(metadata.GUID);
            Info.Instance = this;
            
            Logger = BepInEx.Logging.Logger.CreateLogSource(metadata.Name);

            Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
        }
        
        /// <inheritdoc />
        public PluginInfo Info { get; }
        
        /// <inheritdoc />
        public ManualLogSource Logger { get; }

        /// <inheritdoc />
        public ConfigFile Config { get; }

        public Harmony HarmonyInstance { get; set; }

        public abstract void Load();

        public virtual bool Unload() => false;
    }
}
