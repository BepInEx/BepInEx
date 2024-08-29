using System;
using System.IO;

namespace BepInEx.PluginProvider;

public class BepInExPluginLoadContext : IPluginLoadContext
{
    public string AssemblyIdentifier { get; internal set; }
    public string AssemblyHash { get; internal set; }
    private byte[] assemblyData;
    private byte[] assemblySymbolsData;
    public byte[] GetAssemblyData()
    {
        if (assemblyData == null)
        {
            assemblyData = File.ReadAllBytes(AssemblyIdentifier);
        }

        return assemblyData;
    }

    public byte[] GetAssemblySymbolsData()
    {
        if (assemblySymbolsData == null)
        {
            if (!Utility.TryResolveDllSymbols(AssemblyIdentifier, out assemblySymbolsData))
                assemblySymbolsData = null;
        }

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

    public void Dispose()
    {
        assemblyData = null;
    }
}
