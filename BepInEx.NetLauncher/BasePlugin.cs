using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.NetLauncher
{
	public abstract class BasePlugin
	{
		public ManualLogSource Log { get; }

		public ConfigFile Config { get; }

		public HarmonyLib.Harmony HarmonyInstance { get; set; }

		protected BasePlugin()
		{
			var metadata = MetadataHelper.GetMetadata(this);

			HarmonyInstance = new HarmonyLib.Harmony("BepInEx.Plugin." + metadata.GUID);

			Log = Logger.CreateLogSource(metadata.Name);

			Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
		}

		public abstract void Load();

		public virtual bool Unload()
		{
			return false;
		}
	}
}
