using System;

namespace BepInEx.Configuration
{
	public class ConfigWrapper<T>
	{
		public ConfigDefinition Definition { get; protected set; }

		public ConfigFile ConfigFile { get; protected set; }

		/// <summary>
		/// Fired when the setting is changed. Does not detect changes made outside from this object.
		/// </summary>
		public event EventHandler SettingChanged;

		public T Value
		{
			get => TomlTypeConverter.ConvertToValue<T>(ConfigFile.Cache[Definition]);
			set
			{
				ConfigFile.Cache[Definition] = TomlTypeConverter.ConvertToString(value);

				if (ConfigFile.SaveOnConfigSet)
					ConfigFile.Save();

				SettingChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public ConfigWrapper(ConfigFile configFile, ConfigDefinition definition)
		{
			if (!TomlTypeConverter.SupportedTypes.Contains(typeof(T)))
				throw new ArgumentException("Unsupported config wrapper type");

			ConfigFile = configFile;
			Definition = definition;
		}
	}
}