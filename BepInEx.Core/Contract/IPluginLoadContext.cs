using System;

namespace BepInEx;

/// <summary>
///     An interface that allows to dynamically load and track an assembly
///     which can be obtained from a provider
/// </summary>
public interface IPluginLoadContext
{
    /// <summary>
    ///     An identifier that uniquely identifies an assembly from a provider no matter its revision
    /// </summary>
    public string AssemblyIdentifier { get; }
    
    /// <summary>
    ///     An optional hash that changes each time the assembly changes which can be tracked for cache
    ///     invalidation purposes. If this is null, no caching occurs for this assembly load
    /// </summary>
    public string AssemblyHash { get; }
    
    /// <summary>
    ///     Obtains the assembly's raw data without loading it into the appdomain.
    ///     This may be called multiple times by the chainloader
    /// </summary>
    /// <returns>The assembly's raw data in bytes</returns>
    public byte[] GetAssemblyData();

    /// <summary>
    ///     Obtains the assembly's symbols data which will be loaded by the chainloader alongside the assembly
    /// </summary>
    /// <returns>The assembly's symbols data in bytes (either in portable pdb or mdb format). Null if no symbols exists for this assembly</returns>
    public byte[] GetAssemblySymbolsData();
    
    /// <summary>
    ///     Obtains a file's raw data using a relative path from the plugin assembly
    /// </summary>
    /// <param name="relativePath">The relative path from the plugin assembly used to locate the file</param>
    /// <returns>The file's raw data if resolved successfully, null if the file couldn't be loaded</returns>
    public byte[] GetFile(string relativePath);
}
