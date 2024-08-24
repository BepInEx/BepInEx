using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Contract;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes;

namespace BepInEx.Unity.IL2CPP;

public abstract class BasePlugin : IPlugin
{
    protected BasePlugin()
    {
        var metadata = MetadataHelper.GetPluginMetadata(this);

        Info = BaseChainloader<BasePlugin>.GetPluginInfoFromGuid(metadata.GUID);
        Info.Instance = this;
        
        Logger = BepInEx.Logging.Logger.CreateLogSource(metadata.Name);

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
    }

    public PluginInfo Info { get; }
    
    public ManualLogSource Logger { get; }

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
