using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.PluginProvider;

[BepInPluginProvider("BepInExPluginProvider", "BepInExPluginProvider", "1.0")]
internal class BepInExPluginProvider : BasePluginProvider
{
    private static readonly Dictionary<string, string> AssemblyLocationsByFilename = new();
    private static Harmony harmonyInstance;
    
    public override IList<IPluginLoadContext> GetPlugins()
    {
        harmonyInstance = new Harmony(Info.Metadata.GUID);
        harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

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

    [HarmonyPatch]
    public class AssemblyLocationPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Assembly), nameof(Assembly.Location), MethodType.Getter)]
        public static void AssemblyLocationWorkaround(Assembly __instance, ref string __result)
        {
            if (!string.IsNullOrEmpty(__result))
                return;

            var name = __instance.GetName().Name;
            if (!AssemblyLocationsByFilename.TryGetValue(name, out var location)) 
                return;

            var filenameWithoutExtension = Path.Combine(location, name);
            __result = filenameWithoutExtension + ".dll";
        }
    }
}
