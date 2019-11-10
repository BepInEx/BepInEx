using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Provides access to a single setting inside of a <see cref="Configuration.ConfigFile"/>.
	/// </summary>
	/// <typeparam name="T">Type of the setting.</typeparam>
	[Obsolete("Use ConfigFile from new Bind overloads instead")]
	public sealed class ConfigWrapper<T>
	{
		/// <summary>
		/// Entry of this setting in the <see cref="Configuration.ConfigFile"/>.
		/// </summary>
		public ConfigEntry<T> ConfigEntry { get; }

		/// <summary>
		/// Unique definition of this setting.
		/// </summary>
		public ConfigDefinition Definition => ConfigEntry.Definition;

		/// <summary>
		/// Config file this setting is inside of.
		/// </summary>
		public ConfigFile ConfigFile => ConfigEntry.ConfigFile;

		/// <summary>
		/// Fired when the setting is changed. Does not detect changes made outside from this object.
		/// </summary>
		public event EventHandler SettingChanged;

		/// <summary>
		/// Value of this setting.
		/// </summary>
		public T Value
		{
			get => ConfigEntry.Value;
			set => ConfigEntry.Value = value;
		}

		internal ConfigWrapper(ConfigEntry<T> configEntry)
		{
			ConfigEntry = configEntry ?? throw new ArgumentNullException(nameof(configEntry));

			configEntry.ConfigFile.SettingChanged += (sender, args) =>
			{
				if (args.ChangedSetting == configEntry) SettingChanged?.Invoke(sender, args);
			};
		}
	}
}
