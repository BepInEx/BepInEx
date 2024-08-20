using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Patching;

namespace BepInEx.PatcherProvider;

[PatcherProviderPluginInfo("io.bepinex.bepinexpatcherprovider", "BepInExPatcherProvider", "1.0")]
public class BepInExPatcherProvider : BasePatcherProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();

    public override IList<IPluginLoadContext> GetPatchers()
    {
        var loadContexts = new List<IPluginLoadContext>();
        foreach (var dll in Directory.GetFiles(Path.GetFullPath(Paths.PatcherPluginPath), "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                AssemblyLocationsByFilename.Add(Path.GetFileNameWithoutExtension(dll), Path.GetDirectoryName(dll));
                loadContexts.Add(new BepInExPatcherLoadContext
                {
                    AssemblyHash = File.GetLastWriteTimeUtc(dll).ToString("O"),
                    AssemblyIdentifier = dll
                });
            }
            catch (Exception e)
            {
                Log.Log(LogLevel.Error, e);
            }
        }
        
        return loadContexts;
    }

    public override Assembly ResolveAssembly(string name)
    {
        if (!AssemblyLocationsByFilename.TryGetValue(name, out var location))
            return null;

        if (!Utility.TryResolveDllAssembly(new(name), location, out var ass))
            return null;

        return ass;
    }
}
