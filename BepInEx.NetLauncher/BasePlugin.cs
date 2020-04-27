namespace BepInEx.NetLauncher
{
	public abstract class BasePlugin
	{
		protected HarmonyLib.Harmony HarmonyInstance { get; }

		protected BasePlugin()
		{
			//var info = PluginInfoHelper.GetPluginInfo(this);

			//HarmonyInstance = new HarmonyLib.Harmony("BepInEx.Plugin." + info.GUID);
		}

		public abstract void Load();

		public virtual bool Unload()
		{
			return false;
		}
	}
}
