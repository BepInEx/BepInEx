using System;
using System.IO;

namespace BepInEx.PatcherProvider;

public class BepInExPatcherLoadContext : IPluginLoadContext
{
    public string AssemblyIdentifier { get; internal set; }
    public string AssemblyHash { get; internal set; }
    public byte[] GetAssemblyData()
    {
        return File.ReadAllBytes(AssemblyIdentifier);
    }
    
    public byte[] GetAssemblySymbolsData()
    {
        if (Utility.TryResolveAssemblySymbols(AssemblyIdentifier, out var assemblySymbolsData))
            return assemblySymbolsData;

        return assemblySymbolsData;
    }

    public byte[] GetFile(string relativePath)
    {
        if (relativePath == null)
            throw new ArgumentNullException(nameof(relativePath));
        
        string assemblyFolder = Path.GetDirectoryName(AssemblyIdentifier);
        string filePath = Path.Combine(assemblyFolder, relativePath);
        return File.ReadAllBytes(filePath);
    }
}
