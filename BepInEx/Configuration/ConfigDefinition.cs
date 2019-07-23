namespace BepInEx.Configuration
{
	public class ConfigDefinition
	{
		public string Section { get; }

		public string Key { get; }

		public string Description { get; internal set; }

		public ConfigDefinition(string section, string key, string description = null)
		{
			Key = key;
			Section = section;

			Description = description;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;
			if (obj.GetType() != this.GetType())
				return false;

			if (!(obj is ConfigDefinition other))
				return false;

			return string.Equals(Key, other.Key)
				   && string.Equals(Section, other.Section);
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