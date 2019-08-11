using System;

namespace BepInEx.Configuration
{
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

		public override string ToString()
		{
			return Section + " / " + Key;
		}
	}
}