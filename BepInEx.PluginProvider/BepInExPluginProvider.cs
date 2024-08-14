using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace BepInEx.PluginProvider;

[BepInPluginProvider("io.bepinex.bepinexpluginprovider", "BepInExPluginProvider", "1.0")]
public class BepInExPluginProvider : BasePluginProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();
    
    public override IList<IPluginLoader> GetPlugins()
    {
        var loaders = new List<IPluginLoader>();
        foreach (var dll in Directory.GetFiles(Path.GetFullPath(Paths.PluginPath), "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                AssemblyLocationsByFilename.Add(Path.GetFileNameWithoutExtension(dll), Path.GetDirectoryName(dll));
                loaders.Add(new BepInExPluginLoader
                {
                    AssemblyHash = File.GetLastWriteTimeUtc(dll).ToString("O"),
                    AssemblyIdentifier = dll
                });
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, e);
            }
        }
        
        return loaders;
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
