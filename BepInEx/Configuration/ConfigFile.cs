using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace BepInEx.Configuration
{
	/// <summary>
	/// A helper class to handle persistent data.
	/// </summary>
	public class ConfigFile
	{
		private readonly BepInPlugin _ownerMetadata;

		internal static ConfigFile CoreConfig { get; } = new ConfigFile(Paths.BepInExConfigPath, true);

		/// <summary>
		/// All config entries inside 
		/// </summary>
		protected Dictionary<ConfigDefinition, ConfigEntryBase> Entries { get; } = new Dictionary<ConfigDefinition, ConfigEntryBase>();

		private Dictionary<ConfigDefinition, string> HomelessEntries { get; } = new Dictionary<ConfigDefinition, string>();

		/// <summary>
		/// Create a list with all config entries inside of this config file.
		/// </summary>
		[Obsolete("Use GetConfigEntries instead")]
		public ReadOnlyCollection<ConfigDefinition> ConfigDefinitions
		{
			get
			{
				lock (_ioLock) return Entries.Keys.ToList().AsReadOnly();
			}
		}

		/// <summary>
		/// Create an array with all config entries inside of this config file. Should be only used for metadata purposes.
		/// If you want to access and modify an existing setting then use <see cref="GetSetting{T}(ConfigDefinition,T,ConfigDescription)"/> 
		/// instead with no description.
		/// </summary>
		public ConfigEntryBase[] GetConfigEntries()
		{
			lock (_ioLock) return Entries.Values.ToArray();
		}

		/// <summary>
		/// Full path to the config file. The file might not exist until a setting is added and changed, or <see cref="Save"/> is called.
		/// </summary>
		public string ConfigFilePath { get; }

		/// <summary>
		/// If enabled, writes the config to disk every time a value is set. 
		/// If disabled, you have to manually use <see cref="Save"/> or the changes will be lost!
		/// </summary>
		public bool SaveOnConfigSet { get; set; } = true;

		/// <inheritdoc cref="ConfigFile(string, bool, BepInPlugin)"/>
		public ConfigFile(string configPath, bool saveOnInit) : this(configPath, saveOnInit, null) { }

		/// <summary>
		/// Create a new config file at the specified config path.
		/// </summary>
		/// <param name="configPath">Full path to a file that contains settings. The file will be created as needed.</param>
		/// <param name="saveOnInit">If the config file/directory doesn't exist, create it immediately.</param>
		/// <param name="ownerMetadata">Information about the plugin that owns this setting file.</param>
		public ConfigFile(string configPath, bool saveOnInit, BepInPlugin ownerMetadata)
		{
			_ownerMetadata = ownerMetadata;

			if (configPath == null) throw new ArgumentNullException(nameof(configPath));
			configPath = Path.GetFullPath(configPath);
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

		#region Save/Load

		private readonly object _ioLock = new object();
		private bool _disableSaving;

		/// <summary>
		/// Reloads the config from disk. Unsaved changes are lost.
		/// </summary>
		public void Reload()
		{
			lock (_ioLock)
			{
				HomelessEntries.Clear();

				try
				{
					_disableSaving = true;

					string currentSection = string.Empty;

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

						Entries.TryGetValue(definition, out ConfigEntryBase entry);

						if (entry != null)
							entry.SetSerializedValue(currentValue);
						else
							HomelessEntries[definition] = currentValue;
					}
				}
				finally
				{
					_disableSaving = false;
				}
			}

			OnConfigReloaded();
		}

		/// <summary>
		/// Writes the config to disk.
		/// </summary>
		public void Save()
		{
			lock (_ioLock)
			{
				if (_disableSaving) return;

				string directoryName = Path.GetDirectoryName(ConfigFilePath);
				if (directoryName != null) Directory.CreateDirectory(directoryName);

				using (var writer = new StreamWriter(File.Create(ConfigFilePath), Encoding.UTF8))
				{
					if (_ownerMetadata != null)
					{
						writer.WriteLine($"## Settings file was created by plugin {_ownerMetadata.Name} v{_ownerMetadata.Version}");
						writer.WriteLine($"## Plugin GUID: {_ownerMetadata.GUID}");
						writer.WriteLine();
					}

					var allConfigEntries = Entries.Select(x => new { x.Key, entry = x.Value, value = x.Value.GetSerializedValue() })
						.Concat(HomelessEntries.Select(x => new { x.Key, entry = (ConfigEntryBase)null, value = x.Value }));

					foreach (var sectionKv in allConfigEntries.GroupBy(x => x.Key.Section).OrderBy(x => x.Key))
					{
						// Section heading
						writer.WriteLine($"[{sectionKv.Key}]");

						foreach (var configEntry in sectionKv)
						{
							writer.WriteLine();

							configEntry.entry?.WriteDescription(writer);

							writer.WriteLine($"{configEntry.Key.Key} = {configEntry.value}");
						}

						writer.WriteLine();
					}
				}
			}
		}

		#endregion

		#region Wraps

		/// <summary>
		/// Create a new setting or access one of the existing ones. The setting is saved to drive and loaded automatically.
		/// If you are the creator of the setting, provide a ConfigDescription object to give user information about the setting.
		/// If you are using a setting created by another plugin/class, do not provide any ConfigDescription.
		/// </summary>
		/// <typeparam name="T">Type of the value contained in this setting.</typeparam>
		/// <param name="configDefinition">Section and Key of the setting.</param>
		/// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
		/// <param name="configDescription">Description of the setting shown to the user.</param>
		public ConfigWrapper<T> GetSetting<T>(ConfigDefinition configDefinition, T defaultValue, ConfigDescription configDescription = null)
		{
			if (!TomlTypeConverter.CanConvert(typeof(T)))
				throw new ArgumentException($"Type {typeof(T)} is not supported by the config system. Supported types: {string.Join(", ", TomlTypeConverter.GetSupportedTypes().Select(x => x.Name).ToArray())}");

			lock (_ioLock)
			{
				try
				{
					_disableSaving = true;

					Entries.TryGetValue(configDefinition, out var existingEntry);

					if (existingEntry != null && !(existingEntry is ConfigWrapper<T>))
						throw new ArgumentException("The defined setting already exists with a different setting type - " + existingEntry.SettingType.Name);

					var entry = (ConfigWrapper<T>)existingEntry;

					if (entry == null)
					{
						entry = new ConfigWrapper<T>(this, configDefinition, defaultValue);
						Entries[configDefinition] = entry;
					}

					if (configDescription != null)
					{
						if (entry.Description != null)
							Logger.Log(LogLevel.Warning, $"Tried to add configDescription to setting {configDefinition} when it already had one defined. Only add configDescription once or a random one will be used.");
						else
							entry.SetDescription(configDescription);
					}

					if (HomelessEntries.TryGetValue(configDefinition, out string homelessValue))
					{
						entry.SetSerializedValue(homelessValue);
						HomelessEntries.Remove(configDefinition);
					}

					_disableSaving = false;
					if (SaveOnConfigSet)
						Save();

					return entry;
				}
				finally
				{
					_disableSaving = false;
				}
			}
		}

		/// <summary>
		/// Create a new setting or access one of the existing ones. The setting is saved to drive and loaded automatically.
		/// If you are the creator of the setting, provide a ConfigDescription object to give user information about the setting.
		/// If you are using a setting created by another plugin/class, do not provide any ConfigDescription.
		/// </summary>
		/// <typeparam name="T">Type of the value contained in this setting.</typeparam>
		/// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
		/// <param name="key">Name of the setting.</param>
		/// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
		/// <param name="configDescription">Description of the setting shown to the user.</param>
		public ConfigWrapper<T> GetSetting<T>(string section, string key, T defaultValue, ConfigDescription configDescription = null)
			=> GetSetting(new ConfigDefinition(section, key), defaultValue, configDescription);

		/// <inheritdoc cref="GetSetting{T}(string,string,T,ConfigDescription)"/>
		[Obsolete("Use GetSetting instead")]
		public ConfigWrapper<T> Wrap<T>(string section, string key, string description = null, T defaultValue = default(T))
			=> GetSetting(new ConfigDefinition(section ?? "", key), defaultValue, string.IsNullOrEmpty(description) ? null : new ConfigDescription(description));

		/// <inheritdoc cref="GetSetting{T}(ConfigDefinition,T,ConfigDescription)"/>
		[Obsolete("Use GetSetting instead")]
		public ConfigWrapper<T> Wrap<T>(ConfigDefinition configDefinition, T defaultValue = default(T))
			=> GetSetting(configDefinition, defaultValue);

		#endregion

		#region Events

		/// <summary>
		/// An event that is fired every time the config is reloaded.
		/// </summary>
		public event EventHandler ConfigReloaded;

		/// <summary>
		/// Fired when one of the settings is changed.
		/// </summary>
		public event EventHandler<SettingChangedEventArgs> SettingChanged;

		internal void OnSettingChanged(object sender, ConfigEntryBase changedEntryBase)
		{
			if (changedEntryBase == null) throw new ArgumentNullException(nameof(changedEntryBase));

			if (SaveOnConfigSet)
				Save();

			var settingChanged = SettingChanged;
			if (settingChanged == null) return;

			var args = new SettingChangedEventArgs(changedEntryBase);
			foreach (var callback in settingChanged.GetInvocationList().Cast<EventHandler<SettingChangedEventArgs>>())
			{
				try { callback(sender, args); }
				catch (Exception e) { Logger.Log(LogLevel.Error, e); }
			}
		}

		private void OnConfigReloaded()
		{
			var configReloaded = ConfigReloaded;
			if (configReloaded == null) return;

			foreach (var callback in configReloaded.GetInvocationList().Cast<EventHandler>())
			{
				try { callback(this, EventArgs.Empty); }
				catch (Exception e) { Logger.Log(LogLevel.Error, e); }
			}
		}

		#endregion
	}
}
