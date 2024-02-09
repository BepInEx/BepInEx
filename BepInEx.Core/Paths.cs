using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using MonoMod.Utils;
using Version = SemanticVersioning.Version;

namespace BepInEx;

/// <summary>
///     Paths used by BepInEx
/// </summary>
public static class Paths
{

    /// <summary>
    ///     Whether the current environment is Ubisoft Plus. Value is set in <see cref="UbisoftPlusDetected"/>.
    /// </summary>
    private static bool UbisoftPlus { get; set; }

    // TODO: Why is this in Paths?
    /// <summary>
    ///    BepInEx version.
    /// </summary>
    public static Version BepInExVersion { get; } =
        Version.Parse(MetadataHelper.GetAttributes<AssemblyInformationalVersionAttribute>(typeof(Paths).Assembly)[0]
                                    .InformationalVersion);

    /// <summary>
    ///     The path to the Managed folder that contains the main managed assemblies.
    /// </summary>
    public static string ManagedPath { get; private set; }

    /// <summary>
    ///     The path to the game data folder of the currently running Unity game.
    /// </summary>
    public static string GameDataPath { get; private set; }

    /// <summary>
    ///     The directory that the core BepInEx DLLs reside in.
    /// </summary>
    public static string BepInExAssemblyDirectory { get; private set; }

    /// <summary>
    ///     The path to the core BepInEx DLL.
    /// </summary>
    public static string BepInExAssemblyPath { get; private set; }

    /// <summary>
    ///     The path to the main BepInEx folder.
    /// </summary>
    public static string BepInExRootPath { get; private set; }

    /// <summary>
    ///     The path of the currently executing program BepInEx is encapsulated in.
    /// </summary>
    public static string ExecutablePath { get; private set; }

    /// <summary>
    ///     The directory that the currently executing process resides in.
    ///     <para>On OSX however, this is the parent directory of the game.app folder.</para>
    /// </summary>
    public static string GameRootPath { get; private set; }

    /// <summary>
    ///     The path to the config directory.
    /// </summary>
    public static string ConfigPath { get; private set; }

    /// <summary>
    ///     The path to the global BepInEx configuration file.
    /// </summary>
    public static string BepInExConfigPath { get; private set; }

    /// <summary>
    ///     The path to temporary cache files.
    /// </summary>
    public static string CachePath { get; private set; }

    /// <summary>
    ///     The path to the patcher plugin folder which resides in the BepInEx folder.
    /// </summary>
    public static string PatcherPluginPath { get; private set; }

    /// <summary>
    ///     The path to the plugin folder which resides in the BepInEx folder.
    ///     <para>
    ///         This is ONLY guaranteed to be set correctly when Chainloader has been initialized.
    ///     </para>
    /// </summary>
    public static string PluginPath { get; private set; }

    /// <summary>
    ///     The name of the currently executing process.
    /// </summary>
    public static string ProcessName { get; internal set; }

    /// <summary>
    ///     List of directories from where Mono will search assemblies before assembly resolving is invoked.
    /// </summary>
    public static string[] DllSearchPaths { get; private set; }

    
    /// <summary>
    /// Retrieves the subject name of the signer from a digital certificate of a signed file.
    /// </summary>
    /// <param name="filePath">The path to the signed file from which to extract the certificate information.</param>
    /// <returns>
    /// The subject name of the signer if the file is signed and a certificate is found; otherwise, null.
    /// </returns>
    private static string GetExecutableSigner(string filePath)
    {
        try
        {
            var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            return cert.Subject;
        }
        catch
        {
            return null;
        }
    }


    /// <summary>
    /// Determines whether the Ubisoft Plus environment is detected based on several criteria.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the Ubisoft Plus environment is detected; otherwise, <c>false</c>.
    /// </returns>
    internal static bool UbisoftPlusDetected()
    {
        if (UbisoftPlus) return true;
        var signer = GetExecutableSigner(ExecutablePath);
        return signer != null &&
               signer.StartsWith("cn=ubisoft", StringComparison.OrdinalIgnoreCase) &&
               ProcessName.EndsWith("_plus", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(Path.Combine(GameRootPath, "GameAssembly_plus.dll"));
    }

    public static void SetExecutablePath(string executablePath,
                                         string bepinRootPath = null,
                                         string managedPath = null,
                                         bool gameDataRelativeToManaged = false,
                                         string[] dllSearchPath = null)
    {
        ExecutablePath = executablePath;

        ProcessName = Path.GetFileNameWithoutExtension(executablePath);

        GameRootPath = PlatformHelper.Is(Platform.MacOS)
                           ? Utility.ParentDirectory(executablePath, 4)
                           : Path.GetDirectoryName(executablePath);

        if (UbisoftPlusDetected())
        {
            UbisoftPlus = true;
            ProcessName = ProcessName.Replace("_plus", string.Empty);
        }
        else
        {
            UbisoftPlus = false;
        }

        
        GameDataPath = managedPath != null && gameDataRelativeToManaged
                           ? Path.GetDirectoryName(managedPath)
                           : Path.Combine(GameRootPath, $"{ProcessName}_Data");
        ManagedPath = managedPath ?? Path.Combine(GameDataPath, "Managed");
        BepInExRootPath = bepinRootPath ?? Path.Combine(GameRootPath, "BepInEx");
        ConfigPath = Path.Combine(BepInExRootPath, "config");
        BepInExConfigPath = Path.Combine(ConfigPath, "BepInEx.cfg");
        PluginPath = Path.Combine(BepInExRootPath, "plugins");
        PatcherPluginPath = Path.Combine(BepInExRootPath, "patchers");
        BepInExAssemblyDirectory = Path.Combine(BepInExRootPath, "core");
        BepInExAssemblyPath = Path.Combine(BepInExAssemblyDirectory,
                                           $"{Assembly.GetExecutingAssembly().GetName().Name}.dll");
        CachePath = Path.Combine(BepInExRootPath, "cache");
        DllSearchPaths = (dllSearchPath ?? new string[0]).Concat(new[] { ManagedPath }).Distinct().ToArray();
    }

    internal static void SetPluginPath(string pluginPath) =>
        PluginPath = Utility.CombinePaths(BepInExRootPath, pluginPath);
}
