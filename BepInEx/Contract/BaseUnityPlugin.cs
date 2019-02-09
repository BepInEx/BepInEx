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

		protected BaseUnityPlugin()
		{
			var metadata = MetadataHelper.GetMetadata(this);

			Logger = BepInEx.Logger.CreateLogSource(metadata.Name);
		}
	}
}