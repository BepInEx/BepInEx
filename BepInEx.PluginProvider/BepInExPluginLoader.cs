using System.IO;

namespace BepInEx.PluginProvider;

public class BepInExPluginLoader : IPluginLoader
{
    public string AssemblyIdentifier { get; internal set; }
    public string AssemblyHash { get; internal set; }
    private byte[] assemblyData;
    public byte[] GetAssemblyData()
    {
        if (assemblyData == null)
        {
            assemblyData = File.ReadAllBytes(AssemblyIdentifier);
        }

        return assemblyData;
    }
}
