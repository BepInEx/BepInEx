using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;

namespace BepInEx.Configuration
{
	public sealed class ConfigEntry
	{
		internal ConfigEntry(ConfigFile configFile, ConfigDefinition definition, Type settingType, object defaultValue) : this(configFile, definition)
		{
			SetTypeAndDefaultValue(settingType, defaultValue, true);
		}

		internal ConfigEntry(ConfigFile configFile, ConfigDefinition definition)
		{
			ConfigFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
			Definition = definition ?? throw new ArgumentNullException(nameof(definition));
		}

		internal void SetTypeAndDefaultValue(Type settingType, object defaultValue, bool uniqueDefaultValue)
		{
			if (settingType == null) throw new ArgumentNullException(nameof(settingType));

			if (settingType == SettingType)
			{
				if (uniqueDefaultValue)
					DefaultValue = defaultValue;
				return;
			}

			if (SettingType != null)
			{
				throw new ArgumentException($"Tried to define setting \"{Definition}\" as type {settingType.Name} " +
				                            $"while it was already defined as type {SettingType.Name}. Use the same " +
				                            $"Type for all Wrappers of a single setting.");
			}

			if (defaultValue == null && settingType.IsValueType)
				throw new ArgumentException("defaultValue is null while settingType is a value type");

			if (defaultValue != null && !settingType.IsInstanceOfType(defaultValue))
				throw new ArgumentException("defaultValue can not be assigned to type " + settingType.Name);

			SettingType = settingType;
			DefaultValue = defaultValue;
		}

		private object _convertedValue;
		private string _serializedValue;

		public ConfigFile ConfigFile { get; }
		public ConfigDefinition Definition { get; }

		public ConfigDescription Description { get; internal set; }

		public Type SettingType { get; private set; }
		public object DefaultValue { get; private set; }

		/// <summary>
		/// Is the type of this setting defined, and by extension can <see cref="Value"/> of this setting be accessed.
		/// Setting is defined when any <see cref="ConfigWrapper{T}"/> objects reference it.
		/// </summary>
		public bool IsDefined => SettingType != null;

		/// <summary>
		/// Can't be used when <see cref="IsDefined"/> is false.
		/// </summary>
		public object Value
		{
			get
			{
				ProcessSerializedValue();

				return _convertedValue;
			}
			set => SetValue(value, true, this);
		}

		internal void SetValue(object newValue, bool fireEvent, object sender)
		{
			bool wasChanged = ProcessSerializedValue();
			wasChanged = wasChanged || !Equals(newValue, _convertedValue);

			if (wasChanged)
			{
				_convertedValue = newValue;

				if (fireEvent)
					OnSettingChanged(sender);
			}
		}

		public string GetSerializedValue()
		{
			if (_serializedValue != null)
				return _serializedValue;

			if (!IsDefined)
				return null;

			return TomlTypeConverter.ConvertToString(Value, SettingType);
		}

		public void SetSerializedValue(string newValue, bool fireEvent, object sender)
		{
			string current = GetSerializedValue();
			if (string.Equals(current, newValue)) return;

			_serializedValue = newValue;

			if (!IsDefined) return;
			
			if (ProcessSerializedValue())
			{
				if (fireEvent)
					OnSettingChanged(sender);
			}
		}

		private bool ProcessSerializedValue()
		{
			if (!IsDefined)
				throw new InvalidOperationException("Can't get the value before the SettingType is specified");

			if (_serializedValue != null)
			{
				string value = _serializedValue;
				_serializedValue = null;

				if (value != "")
				{
					try
					{
						var newValue = TomlTypeConverter.ConvertToValue(value, SettingType);
						if (!Equals(newValue, _convertedValue))
						{
							_convertedValue = newValue;
							return true;
						}
						return false;
					}
					catch (Exception e)
					{
						Logger.Log(LogLevel.Warning, $"Config value of setting \"{Definition}\" could not be " +
						                             $"parsed and will be ignored. Reason: {e.Message}; Value: {value}");
					}
				}
			}

			if (_convertedValue == null && DefaultValue != null)
			{
				_convertedValue = DefaultValue;
				return true;
			}

			return false;
		}

		private void OnSettingChanged(object sender)
		{
			ConfigFile.OnSettingChanged(sender, this);
		}

		public void WriteDescription(StreamWriter writer)
		{
			if (Description != null)
				writer.WriteLine(Description.ToSerializedString());

			if (SettingType != null)
			{
				writer.WriteLine("# Setting type: " + SettingType.Name);
				writer.WriteLine("# Default value: " + DefaultValue);

				// todo acceptable values

				if (SettingType.IsEnum && SettingType.GetCustomAttributes(typeof(FlagsAttribute), true).Any())
					writer.WriteLine("# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)");
			}
		}
	}
}
