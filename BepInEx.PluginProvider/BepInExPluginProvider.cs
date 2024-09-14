using System.Collections.Generic;
using System.Reflection;
using BepInEx.DefaultProviders;

namespace BepInEx.PluginProvider;

[BepInMetadata("BepInExPluginProvider", "BepInExPluginProvider", "1.0")]
internal class BepInExPluginProvider : GamePluginProvider
{
    public override IList<IPluginLoadContext> GetLoadContexts() =>
        BepInExDefaultProvider.GetLoadContexts(Paths.PluginPath, Logger);

    public override Assembly ResolveAssembly(string name) =>
        BepInExDefaultProvider.ResolveAssembly(name);
}
