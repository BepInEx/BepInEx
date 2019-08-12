using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Arguments for events concerning a change of a setting.
	/// </summary>
	/// <inheritdoc />
	public sealed class SettingChangedEventArgs : EventArgs
	{
		/// <inheritdoc />
		public SettingChangedEventArgs(ConfigEntry changedSetting)
		{
			ChangedSetting = changedSetting;
		}

		/// <summary>
		/// Setting that was changed
		/// </summary>
		public ConfigEntry ChangedSetting { get; }
	}
}