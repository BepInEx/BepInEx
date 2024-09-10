using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace BepInEx.PluginProvider;

[BepInPluginProvider("BepInExPluginProvider", "BepInExPluginProvider", "1.0")]
internal class BepInExPluginProvider : BasePluginProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();
    
    public override IList<IPluginLoadContext> GetPlugins()
    {
        var loadContexts = new List<IPluginLoadContext>();
        foreach (var dll in Directory.GetFiles(Path.GetFullPath(Paths.PluginPath), "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var filename = Path.GetFileNameWithoutExtension(dll);
                var foundDirectory = Path.GetDirectoryName(dll);
                
                // Prioritize the shallowest path of each assembly name
                if (AssemblyLocationsByFilename.TryGetValue(filename, out var existingDirectory))
                {
                    int levelExistingDirectory = existingDirectory?.Count(x => x == Path.DirectorySeparatorChar) ?? 0;
                    int levelFoundDirectory = foundDirectory?.Count(x => x == Path.DirectorySeparatorChar) ?? 0;

                    bool shallowerPathFound = levelExistingDirectory > levelFoundDirectory;
                    Logger.LogWarning($"Found duplicate assemblies filenames: {filename} was found at {foundDirectory} " +
                                      $"while it exists already at {AssemblyLocationsByFilename[filename]}. " +
                                      $"Only the {(shallowerPathFound ? "first" : "second")} will be examined and resolved");
                    
                    if (levelExistingDirectory > levelFoundDirectory)
                        AssemblyLocationsByFilename[filename] = foundDirectory;
                }
                else
                {
                    AssemblyLocationsByFilename.Add(filename, foundDirectory);
                }

                loadContexts.Add(new BepInExPluginLoadContext
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
        
        return loadContexts;
    }

    public override Assembly ResolveAssembly(string name)
    {
        if (!AssemblyLocationsByFilename.TryGetValue(name, out var location))
            return null;

        if (!Utility.TryResolveDllAssemblyWithSymbols(new(name), location, out var ass))
            return null;

        return ass;
    }
}
