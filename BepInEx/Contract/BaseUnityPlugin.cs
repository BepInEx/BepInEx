using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Contract;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx
{
	/// <summary>
	/// The base plugin type that is used by the BepInEx plugin loader.
	/// </summary>
	public abstract class BaseUnityPlugin : MonoBehaviour
	{
		protected ManualLogSource Logger { get; }

		protected ConfigFile Config { get; }

		protected PluginInfo Info { get; }

		protected BaseUnityPlugin()
		{
			var metadata = MetadataHelper.GetMetadata(this);

			if (Chainloader.PluginInfos.TryGetValue(metadata.GUID, out var info))
				Info = info;
			else
				Logging.Logger.LogDebug($"Plugin [{metadata.GUID}] wasn't registered through chainloader! PluginInfo property will not be initialized.");

			Logger = Logging.Logger.CreateLogSource(metadata.Name);

			Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false);
		}
	}
}