using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLogger = HarmonyLib.Tools.Logger;

namespace BepInEx.Logging;

/// <summary>
///     A log source for Harmony messages
/// </summary>
public class HarmonyLogSource : ILogSource
{
    private static readonly ConfigEntry<HarmonyLogger.LogChannel> LogChannels = ConfigFile.CoreConfig.Bind(
     "Harmony.Logger",
     "LogChannels",
     HarmonyLogger.LogChannel.Warn | HarmonyLogger.LogChannel.Error,
     "Specifies which Harmony log channels to listen to.\nNOTE: IL channel dumps the whole patch methods, use only when needed!");

    private static readonly Dictionary<HarmonyLogger.LogChannel, LogLevel> LevelMap = new()
    {
        [HarmonyLogger.LogChannel.Info] = LogLevel.Info,
        [HarmonyLogger.LogChannel.Warn] = LogLevel.Warning,
        [HarmonyLogger.LogChannel.Error] = LogLevel.Error,
        [HarmonyLogger.LogChannel.IL] = LogLevel.Debug
    };

    internal HarmonyLogSource()
    {
        HarmonyLogger.ChannelFilter = LogChannels.Value;
        HarmonyLogger.MessageReceived += HandleHarmonyMessage;
    }

    /// <inheritdoc />
    public void Dispose() => HarmonyLogger.MessageReceived -= HandleHarmonyMessage;

    /// <inheritdoc />
    public string SourceName => "HarmonyX";
    
    /// <summary>
    ///     An event invoked when a Harmony message is sent with a channel this log source listens to
    /// </summary>
    public event EventHandler<LogEventArgs> LogEvent;

    private void HandleHarmonyMessage(object sender, HarmonyLogger.LogEventArgs e)
    {
        if (!LevelMap.TryGetValue(e.LogChannel, out var level))
            return;

        LogEvent?.Invoke(this, new LogEventArgs(e.Message, level, this));
    }
}
