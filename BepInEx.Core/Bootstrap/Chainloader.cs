using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Core.Bootstrap;
using BepInEx.Logging;
using MonoMod.Utils;

namespace BepInEx;

/// <summary>
///     The base type of any chainloaders no matter the runtime
/// </summary>
public abstract class Chainloader : LoadingSystem<GamePluginProvider, GamePlugin>
{
    /// <inheritdoc />
    protected override string PluginLogName => "plugin";

    /// <inheritdoc />
    protected override string LoadCacheName => "chainloader";

    private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "AppendLog",
     false,
     "Appends to the log file instead of overwriting, on game startup.");

    private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "Enabled",
     true,
     "Enables writing log messages to disk.");

    private static readonly ConfigEntry<LogLevel> ConfigDiskLoggingDisplayedLevel = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "LogLevels",
     LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
     "Only displays the specified log levels in the disk log output.");

    private static readonly ConfigEntry<bool> ConfigDiskLoggingInstantFlushing = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "InstantFlushing",
     false,
     new StringBuilder()
         .AppendLine("If true, instantly writes any received log entries to disk.")
         .AppendLine("This incurs a major performance hit if a lot of log messages are being written, however it is really useful for debugging crashes.")
         .ToString());

    private static readonly ConfigEntry<int> ConfigDiskLoggingFileLimit = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "ConcurrentFileLimit",
     5,
     new StringBuilder()
         .AppendLine("The maximum amount of concurrent log files that will be written to disk.")
         .AppendLine("As one log file is used per open game instance, you may find it necessary to increase this limit when debugging multiple instances at the same time.")
         .ToString());

    internal Chainloader() { }

    private static readonly Dictionary<string, PluginInfo> LoadedPatchers = new();
    
    /// <summary>
    /// The current chainloader instance
    /// </summary>
    public static Chainloader Instance { get; protected set; }
    
    /// <summary>
    ///     Occurs after a plugin is instantiated and just before <see cref="GamePlugin.Load"/> is called.
    /// </summary>
    public static event Action<PluginLoadEventArgs> PluginLoad;

    internal static void SetLoadedPatchers(Dictionary<string, PluginInfo> patchers)
    {
        LoadedPatchers.AddRange(patchers);
    }
    
    internal virtual void Initialize(string gameExePath = null)
    {
        if (Initialized)
            throw new InvalidOperationException("Chainloader cannot be initialized multiple times");

        Instance = this;
        
        // Set vitals
        if (gameExePath != null)
            // Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
            // This is used by Preloader to use environment variables, for example
            Paths.SetExecutablePath(gameExePath);

        InitializeLoggers();

        if (!Directory.Exists(Paths.PluginProviderPath))
            Directory.CreateDirectory(Paths.PluginProviderPath);

        if (!Directory.Exists(Paths.PluginPath))
            Directory.CreateDirectory(Paths.PluginPath);

        Initialized = true;

        Logger.Log(LogLevel.Message, "Chainloader initialized");
    }

    internal virtual void InitializeLoggers()
    {
        if (ConsoleManager.ConsoleEnabled && !ConsoleManager.ConsoleActive)
            ConsoleManager.CreateConsole();

        if (ConsoleManager.ConsoleActive)
        {
            if (!Logger.Listeners.Any(x => x is ConsoleLogListener))
                Logger.Listeners.Add(new ConsoleLogListener());

            ConsoleManager.SetConsoleTitle(ConsoleTitle);
        }

        if (ConfigDiskLogging.Value)
            Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskLoggingDisplayedLevel.Value,
                                                     ConfigDiskAppend.Value, ConfigDiskLoggingInstantFlushing.Value,
                                                     ConfigDiskLoggingFileLimit.Value));

        if (!TraceLogSource.IsListening)
            Logger.Sources.Add(TraceLogSource.CreateSource());

        if (!Logger.Sources.Any(x => x is HarmonyLogSource))
            Logger.Sources.Add(new HarmonyLogSource());
    }

    /// <inheritdoc />
    internal override void LoadFromProviders()
    {
        Plugins.AddRange(LoadedPatchers);
        base.LoadFromProviders();
        Logger.Log(LogLevel.Message, "Chainloader startup complete");
    }

    /// <inheritdoc />
    protected override GamePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
    {
        var pluginInstance = base.LoadPlugin(pluginInfo, pluginAssembly);
        PluginLoad?.Invoke(new(pluginInfo, pluginAssembly, pluginInstance));
        pluginInstance.Load();

        return pluginInstance;
    }
}
