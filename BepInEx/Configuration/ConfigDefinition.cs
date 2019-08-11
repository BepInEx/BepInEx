using System;

namespace BepInEx.Configuration
{
	public class ConfigDescription
	{
		public ConfigDescription(string description, Type settingType, object defaultValue)
		{
			Description = description ?? throw new ArgumentNullException(nameof(description));
			SettingType = settingType ?? throw new ArgumentNullException(nameof(settingType));
			DefaultValue = defaultValue;

			if(defaultValue == null && settingType.IsByRef)
				throw new ArgumentException("defaultValue is null while settingType is a value type");

			if(defaultValue != null && !settingType.IsInstanceOfType(defaultValue))
				throw new ArgumentException("defaultValue can not be assigned to type " + settingType.Name);
		}

		public string Description { get; }
		public Type SettingType { get; }
		public object DefaultValue { get; }
		//todo value range
	}

	public class ConfigDefinition : IEquatable<ConfigDefinition>
	{
		public string Section { get; }

		public string Key { get; }

		public ConfigDefinition(string section, string key)
		{
			Key = key;
			Section = section;
		}

		public bool Equals(ConfigDefinition other)
		{
			if (other == null) return false;
			return string.Equals(Key, other.Key)
			       && string.Equals(Section, other.Section);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;

			return Equals(obj as ConfigDefinition);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = Key != null ? Key.GetHashCode() : 0;
				hashCode = (hashCode * 397) ^ (Section != null ? Section.GetHashCode() : 0);
				return hashCode;
			}
		}

		public static bool operator ==(ConfigDefinition left, ConfigDefinition right)
			=> Equals(left, right);

		public static bool operator !=(ConfigDefinition left, ConfigDefinition right)
			=> !Equals(left, right);
	}
}