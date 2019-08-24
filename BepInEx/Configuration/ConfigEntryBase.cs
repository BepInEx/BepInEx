using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Container for a single setting of a <see cref="Configuration.ConfigFile"/>. 
	/// Each config entry is linked to one config file.
	/// </summary>
	public abstract class ConfigEntryBase
	{
		/// <summary>
		/// Types of defaultValue and definition.AcceptableValues have to be the same as settingType.
		/// </summary>
		internal ConfigEntryBase(ConfigFile configFile, ConfigDefinition definition, Type settingType, object defaultValue)
		{
			ConfigFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
			Definition = definition ?? throw new ArgumentNullException(nameof(definition));
			SettingType = settingType ?? throw new ArgumentNullException(nameof(settingType));

			// Free type check
			Value = defaultValue;
			DefaultValue = defaultValue;
		}

		/// <summary>
		/// Config file this entry is a part of.
		/// </summary>
		public ConfigFile ConfigFile { get; }

		/// <summary>
		/// Category and name of this setting. Used as a unique key for identification within a <see cref="Configuration.ConfigFile"/>.
		/// </summary>
		public ConfigDefinition Definition { get; }

		/// <summary>
		/// Description / metadata of this setting.
		/// </summary>
		public ConfigDescription Description { get; private set; }

		/// <summary>
		/// Type of the <see cref="Value"/> that this setting holds.
		/// </summary>
		public Type SettingType { get; }

		/// <summary>
		/// Default value of this setting (set only if the setting was not changed before).
		/// </summary>
		public object DefaultValue { get; }

		/// <summary>
		/// Get or set the value of the setting.
		/// </summary>
		public abstract object Value { get; set; }

		/// <summary>
		/// Get the serialized representation of the value.
		/// </summary>
		public string GetSerializedValue()
		{
			return TomlTypeConverter.ConvertToString(Value, SettingType);
		}

		/// <summary>
		/// Set the value by using its serialized form.
		/// </summary>
		public void SetSerializedValue(string value)
		{
			try
			{
				var newValue = TomlTypeConverter.ConvertToValue(value, SettingType);
				Value = newValue;
			}
			catch (Exception e)
			{
				Logger.Log(LogLevel.Warning, $"Config value of setting \"{Definition}\" could not be " +
											 $"parsed and will be ignored. Reason: {e.Message}; Value: {value}");
			}
		}

		internal void SetDescription(ConfigDescription configDescription)
		{
			if (configDescription == null) throw new ArgumentNullException(nameof(configDescription));
			if (configDescription.AcceptableValues != null && !SettingType.IsAssignableFrom(configDescription.AcceptableValues.ValueType))
				throw new ArgumentException("configDescription.AcceptableValues is for a different type than the type of this setting");

			Description = configDescription;

			// Automatically calls ClampValue in case it changed
			Value = Value;
		}

		/// <summary>
		/// If necessary, clamp the value to acceptable value range. T has to be equal to settingType.
		/// </summary>
		protected T ClampValue<T>(T value)
		{
			if (Description?.AcceptableValues != null)
				return (T)Description.AcceptableValues.Clamp(value);
			return value;
		}

		/// <summary>
		/// Trigger setting changed event.
		/// </summary>
		protected void OnSettingChanged(object sender)
		{
			ConfigFile.OnSettingChanged(sender, this);
		}

		/// <summary>
		/// Write a description of this setting using all available metadata.
		/// </summary>
		public void WriteDescription(StreamWriter writer)
		{
			bool hasDescription = Description != null;
			bool hasType = SettingType != null;

			if (hasDescription)
				writer.WriteLine(Description.ToSerializedString());

			if (hasType)
			{
				if (SettingType.IsEnum && SettingType.GetCustomAttributes(typeof(FlagsAttribute), true).Any())
					writer.WriteLine("# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)");

				writer.WriteLine("# Setting type: " + SettingType.Name);

				writer.WriteLine("# Default value: " + DefaultValue);
			}

			if (hasDescription && Description.AcceptableValues != null)
			{
				writer.WriteLine(Description.AcceptableValues.ToSerializedString());
			}
			else if (hasType)
			{
				/*if (SettingType == typeof(bool))
					writer.WriteLine("# Acceptable values: True, False");
				else*/
				if (SettingType.IsEnum)
					writer.WriteLine("# Acceptable values: " + string.Join(", ", Enum.GetNames(SettingType)));
			}
		}
	}
}
