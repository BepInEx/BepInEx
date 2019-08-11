using System;

namespace BepInEx.Configuration
{
	public sealed class SettingChangedEventArgs : EventArgs
	{
		public SettingChangedEventArgs(ConfigEntry changedSetting)
		{
			ChangedSetting = changedSetting;
		}

		public ConfigEntry ChangedSetting { get; }
	}
}