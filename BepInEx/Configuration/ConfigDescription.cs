using System;

namespace BepInEx.Configuration
{
	//todo value range
	/// <summary>
	/// Metadata of a <see cref="ConfigEntry"/>.
	/// </summary>
	public class ConfigDescription
	{
		/// <summary>
		/// Create a new description.
		/// </summary>
		/// <param name="description">Text describing the function of the setting and any notes or warnings.</param>
		public ConfigDescription(string description)
		{
			Description = description ?? throw new ArgumentNullException(nameof(description));
		}

		/// <summary>
		/// Text describing the function of the setting and any notes or warnings.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// Convert the description object into a form suitable for writing into a config file.
		/// </summary>
		public string ToSerializedString()
		{
			return $"# {Description.Replace("\n", "\n# ")}";
		}
	}
}