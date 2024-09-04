using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.NET.Common
{
    public abstract class BasePlugin
    {
        protected BasePlugin()
        {
            var metadata = MetadataHelper.GetMetadata(this);

            HarmonyInstance = new Harmony("BepInEx.Plugin." + metadata.GUID);

            if (BaseChainloader<BasePlugin>.Instance.Plugins.TryGetValue(metadata.GUID, out var pluginInfo))
            {
                Info = pluginInfo;
                Info.Instance = this;
            }
            else
            {
                throw new InvalidOperationException($"The plugin information for {metadata.GUID} couldn't be found on the chainloader");
            }
            
            Log = BepInEx.Logging.Logger.CreateLogSource(metadata.Name);

            Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
        }
        
        /// <inheritdoc />
        public PluginInfo Info { get; }
        
        /// <inheritdoc />
        public ManualLogSource Log { get; }

        /// <inheritdoc />
        public ConfigFile Config { get; }

        public Harmony HarmonyInstance { get; set; }

        public abstract void Load();

        public virtual bool Unload() => false;
    }
}
