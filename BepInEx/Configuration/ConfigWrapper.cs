using System;

namespace BepInEx.Configuration
{
	public sealed class ConfigWrapper<T>
	{
		public ConfigEntry ConfigEntry { get; }

		public ConfigDefinition Definition => ConfigEntry.Definition;
		public ConfigFile ConfigFile => ConfigEntry.ConfigFile;

		/// <summary>
		/// Fired when the setting is changed. Does not detect changes made outside from this object.
		/// </summary>
		public event EventHandler SettingChanged;

		public T Value
		{
			get => (T)ConfigEntry.Value;
			set => ConfigEntry.SetValue(value, true, this);
		}

		internal ConfigWrapper(ConfigEntry configEntry)
		{
			ConfigEntry = configEntry ?? throw new ArgumentNullException(nameof(configEntry));

			configEntry.ConfigFile.SettingChanged += (sender, args) =>
			{
				if (args.ChangedSetting == configEntry) SettingChanged?.Invoke(sender, args);
			};
		}
	}
}