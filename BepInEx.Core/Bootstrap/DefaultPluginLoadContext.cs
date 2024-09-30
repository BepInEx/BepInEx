﻿using System;
using System.IO;

namespace BepInEx.Core.Bootstrap;

internal class DefaultPluginLoadContext : IPluginLoadContext
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
        string filePath = assemblyFolder is null ? relativePath : Path.Combine(assemblyFolder, relativePath);
        return File.ReadAllBytes(filePath);
    }
}
