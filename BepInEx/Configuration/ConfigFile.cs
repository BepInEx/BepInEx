using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BepInEx.Configuration
{
	/// <summary>
	/// A helper class to handle persistent data.
	/// </summary>
	public class ConfigFile
	{
		private static readonly Regex sanitizeKeyRegex = new Regex(@"[^a-zA-Z0-9\-\.]+");

		internal static ConfigFile CoreConfig { get; } = new ConfigFile(Paths.BepInExConfigPath, true);

		protected internal Dictionary<ConfigDefinition, string> Cache { get; } = new Dictionary<ConfigDefinition, string>();

		public ReadOnlyCollection<ConfigDefinition> ConfigDefinitions => Cache.Keys.ToList().AsReadOnly();

		/// <summary>
		/// An event that is fired every time the config is reloaded.
		/// </summary>
		public event EventHandler ConfigReloaded;

		public string ConfigFilePath { get; }

		/// <summary>
		/// If enabled, writes the config to disk every time a value is set.
		/// </summary>
		public bool SaveOnConfigSet { get; set; } = true;

		public ConfigFile(string configPath, bool saveOnInit)
		{
			ConfigFilePath = configPath;

			if (File.Exists(ConfigFilePath))
			{
				Reload();
			}
			else if (saveOnInit)
			{
				Save();
			}
		}

		private object _ioLock = new object();

		/// <summary>
		/// Reloads the config from disk. Unsaved changes are lost.
		/// </summary>
		public void Reload()
		{
			lock (_ioLock)
			{
				Dictionary<ConfigDefinition, string> descriptions = Cache.ToDictionary(x => x.Key, x => x.Key.Description);

				string currentSection = "";

				foreach (string rawLine in File.ReadAllLines(ConfigFilePath))
				{
					string line = rawLine.Trim();

					if (line.StartsWith("#")) //comment
						continue;

					if (line.StartsWith("[") && line.EndsWith("]")) //section
					{
						currentSection = line.Substring(1, line.Length - 2);
						continue;
					}

					string[] split = line.Split('='); //actual config line
					if (split.Length != 2)
						continue; //empty/invalid line

					string currentKey = split[0].Trim();
					string currentValue = split[1].Trim();

					var definition = new ConfigDefinition(currentSection, currentKey);

					if (descriptions.ContainsKey(definition))
						definition.Description = descriptions[definition];

					Cache[definition] = currentValue;
				}

				ConfigReloaded?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Writes the config to disk.
		/// </summary>
		public void Save()
		{
			lock (_ioLock)
			{
				if (!Directory.Exists(Paths.ConfigPath))
					Directory.CreateDirectory(Paths.ConfigPath);

				using (StreamWriter writer = new StreamWriter(File.Create(ConfigFilePath), System.Text.Encoding.UTF8))
					foreach (var sectionKv in Cache.GroupBy(x => x.Key.Section).OrderBy(x => x.Key))
					{
						writer.WriteLine($"[{sectionKv.Key}]");

						foreach (var entryKv in sectionKv)
						{
							writer.WriteLine();

							if (!string.IsNullOrEmpty(entryKv.Key.Description))
								writer.WriteLine($"# {entryKv.Key.Description.Replace("\n", "\n# ")}");

							writer.WriteLine($"{entryKv.Key.Key} = {entryKv.Value}");
						}

						writer.WriteLine();
					}
			}
		}

		public ConfigWrapper<T> Wrap<T>(ConfigDefinition configDefinition, T defaultValue = default(T))
		{
			if (!Cache.ContainsKey(configDefinition))
			{
				Cache.Add(configDefinition, TomlTypeConverter.ConvertToString(defaultValue));
				Save();
			}
			else
			{
				var original = Cache.Keys.First(x => x.Equals(configDefinition));

				if (original.Description != configDefinition.Description)
				{
					original.Description = configDefinition.Description;
					Save();
				}
			}

			return new ConfigWrapper<T>(this, configDefinition);
		}

		public ConfigWrapper<T> Wrap<T>(string section, string key, string description = null, T defaultValue = default(T))
			=> Wrap<T>(new ConfigDefinition(section, key, description), defaultValue);
	}
}