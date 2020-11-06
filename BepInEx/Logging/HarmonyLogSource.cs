using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLogger = HarmonyLib.Tools.Logger;

namespace BepInEx.Logging
{
	internal class HarmonyLogSource : ILogSource
	{
		private static readonly ConfigEntry<HarmonyLogger.LogChannel> LogChannels = ConfigFile.CoreConfig.Bind(
			"Harmony.Logger",
			"LogChannels",
			HarmonyLogger.LogChannel.Warn | HarmonyLogger.LogChannel.Error,
			"Specifies which Harmony log channels to listen to.\nNOTE: IL channel dumps the whole patch methods, use only when needed!");

		private static readonly Dictionary<HarmonyLogger.LogChannel, LogLevel> LevelMap = new Dictionary<HarmonyLogger.LogChannel, LogLevel>
		{
			[HarmonyLogger.LogChannel.Info] = LogLevel.Info,
			[HarmonyLogger.LogChannel.Warn] = LogLevel.Warning,
			[HarmonyLogger.LogChannel.Error] = LogLevel.Error,
			[HarmonyLogger.LogChannel.IL] = LogLevel.Debug
		};
		
		public HarmonyLogSource()
		{
			HarmonyLogger.ChannelFilter = LogChannels.Value;			
			HarmonyLogger.MessageReceived += HandleHarmonyMessage;
		}

		private void HandleHarmonyMessage(object sender, HarmonyLib.Tools.Logger.LogEventArgs e)
		{
			if (!LevelMap.TryGetValue(e.LogChannel, out var level))
				return;

			LogEvent?.Invoke(this, new LogEventArgs(e.Message, level, this));
		}

		public void Dispose()
		{
			HarmonyLogger.MessageReceived -= HandleHarmonyMessage;
		}

		public string SourceName { get; } = "HarmonyX";
		public event EventHandler<LogEventArgs> LogEvent;
	}
}