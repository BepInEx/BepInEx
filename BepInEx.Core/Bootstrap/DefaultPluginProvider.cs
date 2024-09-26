using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace BepInEx.Core.Bootstrap;

internal class DefaultPluginProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();
    
    internal void Initialize()
    {
        Logger.Log(LogLevel.Message, "Started Initialise of default provider");
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveAssembly(args.Name);
        PhaseManager.Instance.OnPhaseStarted += phase =>
        {
            Logger.Log(LogLevel.Message, $"Providing on phase {phase}");
            PluginManager.Instance.Providers.Add(new(), GetLoadContexts);
        };
        Logger.Log(LogLevel.Message, "Ended Initialise of default provider");
    }

    private IList<IPluginLoadContext> GetLoadContexts()
    {
        var loadContexts = new List<IPluginLoadContext>();
        foreach (var dll in Directory.GetFiles(Path.GetFullPath(Paths.PluginPath), "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var filename = Path.GetFileNameWithoutExtension(dll);
                var foundDirectory = Path.GetDirectoryName(dll);
                
                // Prioritize the shallowest path of each assembly name
                if (PhaseManager.Instance.CurrentPhase == BepInPhases.EntrypointPhase
                 && AssemblyLocationsByFilename.TryGetValue(filename, out var existingDirectory))
                {
                    int levelExistingDirectory = existingDirectory?.Count(x => x == Path.DirectorySeparatorChar) ?? 0;
                    int levelFoundDirectory = foundDirectory?.Count(x => x == Path.DirectorySeparatorChar) ?? 0;
                    
                    bool shallowerPathFound = levelExistingDirectory > levelFoundDirectory;
                    Logger.Log(LogLevel.Warning, $"Found duplicate assemblies filenames: {filename} was found at {foundDirectory} " +
                                                    $"while it exists already at {AssemblyLocationsByFilename[filename]}. " +
                                                    $"Only the {(shallowerPathFound ? "first" : "second")} will be examined and resolved");
                    
                    if (levelExistingDirectory > levelFoundDirectory)
                        AssemblyLocationsByFilename[filename] = foundDirectory;
                }
                else
                {
                    AssemblyLocationsByFilename[filename] = foundDirectory;
                }
                
                loadContexts.Add(new DefaultPluginLoadContext
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

    private Assembly ResolveAssembly(string name)
    {
        if (!AssemblyLocationsByFilename.TryGetValue(name, out var location))
            return null;

        if (!Utility.TryResolveDllAssemblyWithSymbols(new(name), location, out var ass))
            return null;

        return ass;
    }
}
