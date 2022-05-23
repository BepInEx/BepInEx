using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes;

namespace BepInEx.IL2CPP;

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

    /// <summary>
    ///     Add a Component (e.g. MonoBehaviour) into Unity scene.
    ///     Automatically registers the type with Il2Cpp Type system if it isn't already.
    /// </summary>
    /// <typeparam name="T">Type of the component to add.</typeparam>
    public T AddComponent<T>() where T : Il2CppObjectBase => IL2CPPChainloader.AddUnityComponent<T>();
}
