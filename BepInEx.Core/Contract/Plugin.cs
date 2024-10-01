﻿using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx;

/// <summary>
///     The base type of all plugins and providers that BepInEx can dynamically load via <see cref="PluginManager"/>
/// </summary>
public abstract class Plugin
{
    /// <summary>
    ///     Information about this plugin as it was loaded.
    /// </summary>
    public PluginInfo Info { get; internal set; }

    /// <summary>
    ///     Logger source tied to this plugin.
    /// </summary>
    public ManualLogSource Logger { get; internal set; }

    /// <summary>
    ///     Default config file tied to this plugin. The config file will not be created until
    ///     any settings are added and changed, or <see cref="ConfigFile.Save" /> is called.
    /// </summary>
    public ConfigFile Config { get; internal set; }

    /// <summary>
    ///     A callback called when loaded by the chainloader
    /// </summary>
    public abstract void Load();
}