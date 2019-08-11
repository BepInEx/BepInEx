using System;

namespace BepInEx.Configuration
{
	public class ConfigDescription
	{
		public ConfigDescription(string description)
		{
			Description = description ?? throw new ArgumentNullException(nameof(description));
		}

		public string Description { get; }

		//todo value range

		public string ToSerializedString()
		{
			return $"# {Description.Replace("\n", "\n# ")}";
		}
	}
}