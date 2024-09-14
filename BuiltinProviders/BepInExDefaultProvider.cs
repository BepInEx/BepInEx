using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace BepInEx.DefaultProviders;

internal static class BepInExDefaultProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();

    internal static IList<IPluginLoadContext> GetLoadContexts(string directory, ManualLogSource logger)
    {
        var loadContexts = new List<IPluginLoadContext>();
        foreach (var dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
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
                    logger.LogWarning($"Found duplicate assemblies filenames: {filename} was found at {foundDirectory} " +
                                      $"while it exists already at {AssemblyLocationsByFilename[filename]}. " +
                                      $"Only the {(shallowerPathFound ? "first" : "second")} will be examined and resolved");
                    
                    if (levelExistingDirectory > levelFoundDirectory)
                        AssemblyLocationsByFilename[filename] = foundDirectory;
                }
                else
                {
                    AssemblyLocationsByFilename.Add(filename, foundDirectory);
                }
                
                loadContexts.Add(new BepInExDefaultProviderLoadContext
                {
                    AssemblyHash = File.GetLastWriteTimeUtc(dll).ToString("O"),
                    AssemblyIdentifier = dll
                });
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, e);
            }
        }
        
        return loadContexts;
    }

    internal static Assembly ResolveAssembly(string name)
    {
        if (!AssemblyLocationsByFilename.TryGetValue(name, out var location))
            return null;

        if (!Utility.TryResolveDllAssemblyWithSymbols(new(name), location, out var ass))
            return null;

        return ass;
    }
}
