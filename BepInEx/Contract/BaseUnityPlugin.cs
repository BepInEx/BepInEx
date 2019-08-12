using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx
{
	/// <summary>
	/// The base plugin type that is used by the BepInEx plugin loader.
	/// </summary>
	public abstract class BaseUnityPlugin : MonoBehaviour
	{
		/// <summary>
		/// Information about this plugin as it was loaded.
		/// </summary>
		public BepInPlugin Metadata { get; }

		/// <summary>
		/// Logger instance tied to this plugin.
		/// </summary>
		protected ManualLogSource Logger { get; }

		/// <summary>
		/// Default config file tied to this plugin. The config file will not be created until 
		/// any settings are added and changed, or <see cref="ConfigFile.Save"/> is called.
		/// </summary>
		protected ConfigFile Config { get; }

		/// <summary>
		/// Create a new instance of a plugin and all of its tied in objects.
		/// </summary>
		protected BaseUnityPlugin()
		{
			Metadata = MetadataHelper.GetMetadata(this);

			Logger = Logging.Logger.CreateLogSource(Metadata.Name);

			Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, Metadata.GUID + ".cfg"), false, this);
		}
	}
}