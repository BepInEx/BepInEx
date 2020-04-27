using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Metadata of a <see cref="ConfigEntryBase"/>.
	/// </summary>
	public class ConfigDescription
	{
		/// <summary>
		/// Create a new description.
		/// </summary>
		/// <param name="description">Text describing the function of the setting and any notes or warnings.</param>
		/// <param name="acceptableValues">Range of values that this setting can take. The setting's value will be automatically clamped.</param>
		/// <param name="tags">Objects that can be used by user-made classes to add functionality.</param>
		public ConfigDescription(string description, AcceptableValueBase acceptableValues = null, params object[] tags)
		{
			AcceptableValues = acceptableValues;
			Tags = tags;
			Description = description ?? throw new ArgumentNullException(nameof(description));
		}

		/// <summary>
		/// Text describing the function of the setting and any notes or warnings.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// Range of acceptable values for a setting.
		/// </summary>
		public AcceptableValueBase AcceptableValues { get; }

		/// <summary>
		/// Objects that can be used by user-made classes to add functionality.
		/// </summary>
		public object[] Tags { get; }

		/// <summary>
		/// An empty description.
		/// </summary>
		public static ConfigDescription Empty { get; } = new ConfigDescription("");
	}
}
