using System.Collections.Generic;
using System.Reflection;
using BepInEx.DefaultProviders;
using BepInEx.Preloader.Core.Patching;

namespace BepInEx.PatcherProvider;

[BepInMetadata("BepInExPatcherProvider", "BepInExPatcherProvider", "1.0")]
internal class BepInExPatcherPluginProvider : PatcherPluginProvider
{
    public override IList<IPluginLoadContext> GetLoadContexts() =>
        BepInExDefaultProvider.GetLoadContexts(Paths.PatcherPluginPath, Logger);

    public override Assembly ResolveAssembly(string name) =>
        BepInExDefaultProvider.ResolveAssembly(name);
}
