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
		// Need to be lazy evaluated to not cause problems for unit tests
		private static ConfigFile _coreConfig;
		internal static ConfigFile CoreConfig => _coreConfig ?? (_coreConfig = new ConfigFile(Paths.BepInExConfigPath, true));

		protected Dictionary<ConfigDefinition, ConfigEntry> Entries { get; } = new Dictionary<ConfigDefinition, ConfigEntry>();

		[Obsolete("Use ConfigEntries instead")]
		public ReadOnlyCollection<ConfigDefinition> ConfigDefinitions => Entries.Keys.ToList().AsReadOnly();

		public ReadOnlyCollection<ConfigEntry> ConfigEntries => Entries.Values.ToList().AsReadOnly();

		public string ConfigFilePath { get; }

		/// <summary>
		/// If enabled, writes the config to disk every time a value is set. If disabled, you have to manually save or the changes will be lost!
		/// </summary>
		public bool SaveOnConfigSet { get; set; } = true;

		public ConfigFile(string configPath, bool saveOnInit)
		{
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

			StartWatching();
		}

		#region Save/Load

		private readonly object _ioLock = new object();

		/// <summary>
		/// Reloads the config from disk. Unsaved changes are lost.
		/// </summary>
		public void Reload()
		{
			lock (_ioLock)
			{
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

					Entries.TryGetValue(definition, out ConfigEntry entry);
					if (entry == null)
					{
						entry = new ConfigEntry(this, definition);
						Entries[definition] = entry;
					}

					entry.SetSerializedValue(currentValue, true, this);
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
				StopWatching();

				string directoryName = Path.GetDirectoryName(ConfigFilePath);
				if (directoryName != null) Directory.CreateDirectory(directoryName);

				using (var writer = new StreamWriter(File.Create(ConfigFilePath), Encoding.UTF8))
				{
					foreach (var sectionKv in Entries.GroupBy(x => x.Key.Section).OrderBy(x => x.Key))
					{
						// Section heading
						writer.WriteLine($"[{sectionKv.Key}]");

						foreach (var configEntry in sectionKv.Select(x => x.Value))
						{
							writer.WriteLine();

							configEntry.WriteDescription(writer);

							writer.WriteLine($"{configEntry.Definition.Key} = {configEntry.GetSerializedValue()}");
						}

						writer.WriteLine();
					}
				}

				StartWatching();
			}
		}

		#endregion

		#region Wraps

		public ConfigWrapper<T> Wrap<T>(ConfigDefinition configDefinition, T defaultValue, ConfigDescription configDescription = null)
		{
			if (!TomlTypeConverter.CanConvert(typeof(T)))
				throw new ArgumentException($"Type {typeof(T)} is not supported by the config system. Supported types: {string.Join(", ", TomlTypeConverter.GetSupportedTypes().Select(x => x.Name).ToArray())}");

			Entries.TryGetValue(configDefinition, out var entry);

			if (entry == null)
			{
				entry = new ConfigEntry(this, configDefinition, typeof(T), defaultValue);
				Entries[configDefinition] = entry;
			}
			else
			{
				entry.SetTypeAndDefaultValue(typeof(T), defaultValue, !Equals(defaultValue, default(T)));
			}

			if (configDescription != null)
			{
				if (entry.Description != null)
					Logger.Log(LogLevel.Warning, $"Tried to add configDescription to setting {configDefinition} when it already had one defined. Only add configDescription once or a random one will be used.");

				entry.Description = configDescription;
			}

			return new ConfigWrapper<T>(entry);
		}

		[Obsolete("Use other Wrap overloads instead")]
		public ConfigWrapper<T> Wrap<T>(string section, string key, string description = null, T defaultValue = default(T))
			=> Wrap(new ConfigDefinition(section, key), defaultValue, string.IsNullOrEmpty(description) ? null : new ConfigDescription(description));

		public ConfigWrapper<T> Wrap<T>(string section, string key, T defaultValue, ConfigDescription configDescription = null)
			=> Wrap(new ConfigDefinition(section, key), defaultValue, configDescription);

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

		protected internal void OnSettingChanged(object sender, ConfigEntry changedEntry)
		{
			if (changedEntry == null) throw new ArgumentNullException(nameof(changedEntry));

			if (SettingChanged != null)
			{
				var args = new SettingChangedEventArgs(changedEntry);

				foreach (var callback in SettingChanged.GetInvocationList().Cast<EventHandler<SettingChangedEventArgs>>())
				{
					try
					{
						callback(sender, args);
					}
					catch (Exception e)
					{
						Logger.Log(LogLevel.Error, e);
					}
				}
			}

			// todo better way to prevent write loop? maybe do some caching?
			if (sender != this && SaveOnConfigSet)
				Save();
		}

		protected void OnConfigReloaded()
		{
			if (ConfigReloaded != null)
			{
				foreach (var callback in ConfigReloaded.GetInvocationList().Cast<EventHandler>())
				{
					try
					{
						callback(this, EventArgs.Empty);
					}
					catch (Exception e)
					{
						Logger.Log(LogLevel.Error, e);
					}
				}
			}
		}

		#endregion

		#region File watcher

		private FileSystemWatcher _watcher;

		/// <summary>
		/// Start watching the config file on disk for changes.
		/// </summary>
		public void StartWatching()
		{
			lock (_ioLock)
			{
				if (_watcher != null) return;

				_watcher = new FileSystemWatcher
				{
					Path = Path.GetDirectoryName(ConfigFilePath) ?? throw new ArgumentException("Invalid config path"),
					Filter = Path.GetFileName(ConfigFilePath),
					IncludeSubdirectories = false,
					NotifyFilter = NotifyFilters.LastWrite,
					EnableRaisingEvents = true
				};

				_watcher.Changed += (sender, args) => Reload();
			}
		}

		/// <summary>
		/// Stop watching the config file on disk for changes.
		/// </summary>
		public void StopWatching()
		{
			lock (_ioLock)
			{
				_watcher?.Dispose();
				_watcher = null;
			}
		}

		~ConfigFile()
		{
			StopWatching();
		}

		#endregion
	}
}
