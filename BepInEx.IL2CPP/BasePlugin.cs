using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.IL2CPP
{
    public abstract class BasePlugin
    {
        protected BasePlugin()
        {
            var metadata = MetadataHelper.GetMetadata(this);

            Log = Logger.CreateLogSource(metadata.Name);

            Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
        }

        public ManualLogSource Log { get; }

        public ConfigFile Config { get; }

        public abstract void Load();

        public virtual bool Unload() => false;
    }
}
