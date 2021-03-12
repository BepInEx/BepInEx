using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace BepInEx.Configuration
{
    /// <summary>
    ///     A helper class to handle persistent data. All public methods are thread-safe.
    /// </summary>
    public class ConfigFile : IDictionary<ConfigDefinition, ConfigEntryBase>
    {
        private readonly BepInPlugin _ownerMetadata;

        /// <inheritdoc cref="ConfigFile(string, bool, BepInPlugin)" />
        public ConfigFile(string configPath, bool saveOnInit) : this(configPath, saveOnInit, null) { }

        /// <summary>
        ///     Create a new config file at the specified config path.
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
                Reload();
            else if (saveOnInit) Save();
        }

        public static ConfigFile CoreConfig { get; } = new(Paths.BepInExConfigPath, true);

        /// <summary>
        ///     All config entries inside
        /// </summary>
        protected Dictionary<ConfigDefinition, ConfigEntryBase> Entries { get; } = new();

        /// <summary>
        /// Readonly collection of all orphaned config entries in this config file.
        /// </summary>
        public ReadOnlyCollection<ConfigDefinition> OrphanedEntries
        {
            get
            {
                lock (_ioLock)
                    return _orphanedEntries.Keys.ToList().AsReadOnly();
            }
        }

        private Dictionary<ConfigDefinition, string> _orphanedEntries = new();

        /// <summary>
        ///     Create a list with all config entries inside of this config file.
        /// </summary>
        [Obsolete("Use Keys instead")]
        public ReadOnlyCollection<ConfigDefinition> ConfigDefinitions
        {
            get
            {
                lock (_ioLock) return Entries.Keys.ToList().AsReadOnly();
            }
        }

        /// <summary>
        ///     Full path to the config file. The file might not exist until a setting is added and changed, or <see cref="Save" />
        ///     is called.
        /// </summary>
        public string ConfigFilePath { get; }

        /// <summary>
        ///     If enabled, writes the config to disk every time a value is set.
        ///     If disabled, you have to manually use <see cref="Save" /> or the changes will be lost!
        /// </summary>
        public bool SaveOnConfigSet { get; set; } = true;

        /// <inheritdoc />
        public ConfigEntryBase this[ConfigDefinition key]
        {
            get
            {
                lock (_ioLock)
                    return Entries[key];
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        public ConfigEntryBase this[string section, string key] => this[new ConfigDefinition(section, key)];

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<ConfigDefinition, ConfigEntryBase>> GetEnumerator() =>
            // We can't really do a read lock for this
            Entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>.Add(
            KeyValuePair<ConfigDefinition, ConfigEntryBase> item)
        {
            lock (_ioLock)
                Entries.Add(item.Key, item.Value);
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<ConfigDefinition, ConfigEntryBase> item)
        {
            lock (_ioLock)
                return ((ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>) Entries).Contains(item);
        }

        void ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>.CopyTo(
            KeyValuePair<ConfigDefinition, ConfigEntryBase>[] array,
            int arrayIndex)
        {
            lock (_ioLock)
                ((ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>) Entries).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>.Remove(
            KeyValuePair<ConfigDefinition, ConfigEntryBase> item)
        {
            lock (_ioLock)
                return Entries.Remove(item.Key);
        }

        /// <inheritdoc />
        public int Count
        {
            get
            {
                lock (_ioLock)
                    return Entries.Count;
            }
        }

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public bool ContainsKey(ConfigDefinition key)
        {
            lock (_ioLock)
                return Entries.ContainsKey(key);
        }

        /// <inheritdoc />
        public void Add(ConfigDefinition key, ConfigEntryBase value) =>
            throw new InvalidOperationException("Directly adding a config entry is not supported");

        /// <inheritdoc />
        public bool Remove(ConfigDefinition key)
        {
            lock (_ioLock)
                return Entries.Remove(key);
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_ioLock)
                Entries.Clear();
        }

        bool IDictionary<ConfigDefinition, ConfigEntryBase>.TryGetValue(ConfigDefinition key, out ConfigEntryBase value)
        {
            lock (_ioLock)
                return Entries.TryGetValue(key, out value);
        }

        /// <inheritdoc />
        ConfigEntryBase IDictionary<ConfigDefinition, ConfigEntryBase>.this[ConfigDefinition key]
        {
            get
            {
                lock (_ioLock)
                    return Entries[key];
            }
            set => throw new InvalidOperationException("Directly setting a config entry is not supported");
        }

        /// <summary>
        ///     Returns the ConfigDefinitions that the ConfigFile contains.
        ///     <para>Creates a new array when the property is accessed. Thread-safe.</para>
        /// </summary>
        public ICollection<ConfigDefinition> Keys
        {
            get
            {
                lock (_ioLock)
                    return Entries.Keys.ToArray();
            }
        }

        /// <summary>
        ///     Returns the ConfigEntryBase values that the ConfigFile contains.
        ///     <para>Creates a new array when the property is accessed. Thread-safe.</para>
        /// </summary>
        ICollection<ConfigEntryBase> IDictionary<ConfigDefinition, ConfigEntryBase>.Values
        {
            get
            {
                lock (_ioLock)
                    return Entries.Values.ToArray();
            }
        }

        /// <summary>
        ///     Create an array with all config entries inside of this config file. Should be only used for metadata purposes.
        ///     If you want to access and modify an existing setting then use
        ///     <see cref="AddSetting{T}(ConfigDefinition,T,ConfigDescription)" />
        ///     instead with no description.
        /// </summary>
        [Obsolete("Use Values instead")]
        public ConfigEntryBase[] GetConfigEntries()
        {
            lock (_ioLock)
                return Entries.Values.ToArray();
        }

        /// <summary>
        /// Get the value of an orphaned entry.
        /// </summary>
        /// <typeparam name="T">Expected type of the orphaned entry.</typeparam>
        /// <param name="entry">The entry to try and retrieve.</param>
        /// <param name="val">Out value of this entry (if it exists).</param>
        /// <returns>True if the entry exists in the dictionary.</returns>
        public bool TryGetOrphanedEntry<T>(ConfigDefinition entry, out T val)
        {
            lock (_ioLock)
            {
                try
                {
                    if (!_orphanedEntries.TryGetValue(entry, out string serializedVal))
                    {
                        val = default(T);
                        return false;
                    }
                    val = TomlTypeConverter.ConvertToValue<T>(serializedVal);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warning,
                        $"Orphaned config value of setting {entry} could not be parsed and will be ignored. Reason: {e.Message}");
                    val = default(T);
                    return false;
                }
            }
        }

        /// <summary>
        /// Get the value of an orphaned entry.
        /// </summary>
        /// <typeparam name="T">Expected type of the orphaned entry.</typeparam>
        /// <param name="section">Section of the orphaned entry.</param>
        /// <param name="key">Key of the orphaned entry.</param>
        /// <param name="val">Out value of this entry (if it exists).</param>
        /// <returns>True if the entry exists in the dictionary.</returns>
        public bool TryGetOrphanedEntry<T>(string section, string key, out T val)
        {
            return TryGetOrphanedEntry<T>(new ConfigDefinition(section, key), out val);
        }

        /// <summary>
        /// Removes an orphaned config entry from the file.
        /// </summary>
        /// <param name="entry">Orphaned config entry to remove.</param>
        /// <returns>True on success.</returns>
        public bool TryRemoveOrphanedEntry(ConfigDefinition entry)
        {
            lock (_ioLock)
                return _orphanedEntries.Remove(entry);
        }

        /// <summary>
        /// Removes an orphaned config entry from the file.
        /// </summary>
        /// <param name="section">Section of the orphaned entry to remove.</param>
        /// <param name="key">Key of the orphaned entry to remove.</param>
        /// <returns>True on success.</returns>
        public bool TryRemoveOrphanedEntry(string section, string key)
        {
            lock (_ioLock)
                return _orphanedEntries.Remove(new ConfigDefinition(section, key));
        }

        /// <summary>
        /// Removes all orphaned config entries from this file.
        /// </summary>
        public void RemoveAllOrphanedEntries()
        {
            lock (_ioLock)
                _orphanedEntries.Clear();
        }

        #region Save/Load

        private readonly object _ioLock = new();

        /// <summary>
        ///     Generate user-readable comments for each of the settings in the saved .cfg file.
        /// </summary>
        public bool GenerateSettingDescriptions { get; set; } = true;

        /// <summary>
        ///     Reloads the config from disk. Unsaved changes are lost.
        /// </summary>
        public void Reload()
        {
            lock (_ioLock)
            {
                _orphanedEntries.Clear();

                var currentSection = string.Empty;

                foreach (var rawLine in File.ReadAllLines(ConfigFilePath))
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("#")) //comment
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]")) //section
                    {
                        currentSection = line.Substring(1, line.Length - 2);
                        continue;
                    }

                    var split = line.Split(new[] { '=' }, 2); //actual config line
                    if (split.Length != 2)
                        continue; //empty/invalid line

                    var currentKey = split[0].Trim();
                    var currentValue = split[1].Trim();

                    var definition = new ConfigDefinition(currentSection, currentKey);

                    Entries.TryGetValue(definition, out var entry);

                    if (entry != null)
                        entry.SetSerializedValue(currentValue);
                    else
                        _orphanedEntries[definition] = currentValue;
                }
            }

            OnConfigReloaded();
        }

        /// <summary>
        ///     Writes the config to disk.
        /// </summary>
        public void Save()
        {
            lock (_ioLock)
            {
                var directoryName = Path.GetDirectoryName(ConfigFilePath);
                if (directoryName != null) Directory.CreateDirectory(directoryName);

                using (var writer = new StreamWriter(ConfigFilePath, false, Encoding.UTF8))
                {
                    if (_ownerMetadata != null)
                    {
                        writer.WriteLine($"## Settings file was created by plugin {_ownerMetadata.Name} v{_ownerMetadata.Version}");
                        writer.WriteLine($"## Plugin GUID: {_ownerMetadata.GUID}");
                        writer.WriteLine();
                    }

                    var allConfigEntries = Entries
                                           .Select(x => new
                                           {
                                               x.Key, entry = x.Value, value = x.Value.GetSerializedValue()
                                           })
                                           .Concat(_orphanedEntries.Select(x => new
                                           {
                                               x.Key, entry = (ConfigEntryBase) null, value = x.Value
                                           }));

                    foreach (var sectionKv in allConfigEntries.GroupBy(x => x.Key.Section).OrderBy(x => x.Key))
                    {
                        // Section heading
                        writer.WriteLine($"[{sectionKv.Key}]");

                        foreach (var configEntry in sectionKv)
                        {
                            if (GenerateSettingDescriptions)
                            {
                                writer.WriteLine();
                                configEntry.entry?.WriteDescription(writer);
                            }

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
        ///     Access one of the existing settings. If the setting has not been added yet, null is returned.
        ///     If the setting exists but has a different type than T, an exception is thrown.
        ///     New settings should be added with <see cref="AddSetting{T}(ConfigDefinition,T,ConfigDescription)" />.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="configDefinition">Section and Key of the setting.</param>
        [Obsolete("Use ConfigFile[key] or TryGetEntry instead")]
        public ConfigEntry<T> GetSetting<T>(ConfigDefinition configDefinition) =>
            TryGetEntry<T>(configDefinition, out var entry)
                ? entry
                : null;

        /// <summary>
        ///     Access one of the existing settings. If the setting has not been added yet, null is returned.
        ///     If the setting exists but has a different type than T, an exception is thrown.
        ///     New settings should be added with <see cref="AddSetting{T}(ConfigDefinition,T,ConfigDescription)" />.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        [Obsolete("Use ConfigFile[key] or TryGetEntry instead")]
        public ConfigEntry<T> GetSetting<T>(string section, string key) =>
            TryGetEntry<T>(section, key, out var entry)
                ? entry
                : null;

        /// <summary>
        ///     Access one of the existing settings. If the setting has not been added yet, false is returned. Otherwise, true.
        ///     If the setting exists but has a different type than T, an exception is thrown.
        ///     New settings should be added with
        ///     <see cref="Bind{T}(BepInEx.Configuration.ConfigDefinition,T,BepInEx.Configuration.ConfigDescription)" />.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="configDefinition">Section and Key of the setting.</param>
        /// <param name="entry">The ConfigEntry value to return.</param>
        public bool TryGetEntry<T>(ConfigDefinition configDefinition, out ConfigEntry<T> entry)
        {
            lock (_ioLock)
            {
                if (Entries.TryGetValue(configDefinition, out var rawEntry))
                {
                    entry = (ConfigEntry<T>) rawEntry;
                    return true;
                }

                entry = null;
                return false;
            }
        }

        /// <summary>
        ///     Access one of the existing settings. If the setting has not been added yet, null is returned.
        ///     If the setting exists but has a different type than T, an exception is thrown.
        ///     New settings should be added with
        ///     <see cref="Bind{T}(BepInEx.Configuration.ConfigDefinition,T,BepInEx.Configuration.ConfigDescription)" />.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        /// <param name="entry">The ConfigEntry value to return.</param>
        public bool TryGetEntry<T>(string section, string key, out ConfigEntry<T> entry) =>
            TryGetEntry(new ConfigDefinition(section, key), out entry);

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each definition can be used to add only one setting, trying to add a second setting will throw an exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="configDefinition">Section and Key of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="configDescription">Description of the setting shown to the user and other metadata.</param>
        public ConfigEntry<T> Bind<T>(ConfigDefinition configDefinition,
                                      T defaultValue,
                                      ConfigDescription configDescription = null)
        {
            if (!TomlTypeConverter.CanConvert(typeof(T)))
                throw new
                    ArgumentException($"Type {typeof(T)} is not supported by the config system. Supported types: {string.Join(", ", TomlTypeConverter.GetSupportedTypes().Select(x => x.Name).ToArray())}");

            lock (_ioLock)
            {
                if (Entries.TryGetValue(configDefinition, out var rawEntry))
                    return (ConfigEntry<T>) rawEntry;

                var entry = new ConfigEntry<T>(this, configDefinition, defaultValue, configDescription);

                Entries[configDefinition] = entry;

                if (_orphanedEntries.TryGetValue(configDefinition, out var homelessValue))
                {
                    entry.SetSerializedValue(homelessValue);
                    _orphanedEntries.Remove(configDefinition);
                }

                if (SaveOnConfigSet)
                    Save();

                return entry;
            }
        }

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each section and key pair can be used to add only one setting, trying to add a second setting will throw an
        ///     exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="configDescription">Description of the setting shown to the user and other metadata.</param>
        public ConfigEntry<T> Bind<T>(string section,
                                      string key,
                                      T defaultValue,
                                      ConfigDescription configDescription = null) =>
            Bind(new ConfigDefinition(section, key), defaultValue, configDescription);

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each section and key pair can be used to add only one setting, trying to add a second setting will throw an
        ///     exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="description">Simple description of the setting shown to the user.</param>
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description) =>
            Bind(new ConfigDefinition(section, key), defaultValue, new ConfigDescription(description));

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each definition can be used to add only one setting, trying to add a second setting will throw an exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="configDefinition">Section and Key of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="configDescription">Description of the setting shown to the user and other metadata.</param>
        [Obsolete("Use Bind instead")]
        public ConfigEntry<T> AddSetting<T>(ConfigDefinition configDefinition,
                                            T defaultValue,
                                            ConfigDescription configDescription = null) =>
            Bind(configDefinition, defaultValue, configDescription);

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each section and key pair can be used to add only one setting, trying to add a second setting will throw an
        ///     exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="configDescription">Description of the setting shown to the user and other metadata.</param>
        [Obsolete("Use Bind instead")]
        public ConfigEntry<T> AddSetting<T>(string section,
                                            string key,
                                            T defaultValue,
                                            ConfigDescription configDescription = null) =>
            Bind(new ConfigDefinition(section, key), defaultValue, configDescription);

        /// <summary>
        ///     Create a new setting. The setting is saved to drive and loaded automatically.
        ///     Each section and key pair can be used to add only one setting, trying to add a second setting will throw an
        ///     exception.
        /// </summary>
        /// <typeparam name="T">Type of the value contained in this setting.</typeparam>
        /// <param name="section">Section/category/group of the setting. Settings are grouped by this.</param>
        /// <param name="key">Name of the setting.</param>
        /// <param name="defaultValue">Value of the setting if the setting was not created yet.</param>
        /// <param name="description">Simple description of the setting shown to the user.</param>
        [Obsolete("Use Bind instead")]
        public ConfigEntry<T> AddSetting<T>(string section, string key, T defaultValue, string description) =>
            Bind(new ConfigDefinition(section, key), defaultValue, new ConfigDescription(description));

        /// <summary>
        ///     Access a setting. Use Bind instead.
        /// </summary>
        [Obsolete("Use Bind instead")]
        public ConfigWrapper<T> Wrap<T>(string section, string key, string description = null, T defaultValue = default)
        {
            lock (_ioLock)
            {
                var definition = new ConfigDefinition(section, key, description);
                var setting = Bind(definition, defaultValue,
                                   string.IsNullOrEmpty(description) ? null : new ConfigDescription(description));
                return new ConfigWrapper<T>(setting);
            }
        }

        /// <summary>
        ///     Access a setting. Use Bind instead.
        /// </summary>
        [Obsolete("Use Bind instead")]
        public ConfigWrapper<T> Wrap<T>(ConfigDefinition configDefinition, T defaultValue = default) =>
            Wrap(configDefinition.Section, configDefinition.Key, null, defaultValue);

        #endregion

        #region Events

        /// <summary>
        ///     An event that is fired every time the config is reloaded.
        /// </summary>
        public event EventHandler ConfigReloaded;

        /// <summary>
        ///     Fired when one of the settings is changed.
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
            foreach (EventHandler<SettingChangedEventArgs> callback in settingChanged.GetInvocationList())
                try
                {
                    callback(sender, args);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error, e);
                }
        }

        private void OnConfigReloaded()
        {
            var configReloaded = ConfigReloaded;
            if (configReloaded == null) return;

            foreach (EventHandler callback in configReloaded.GetInvocationList())
                try
                {
                    callback(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error, e);
                }
        }

        #endregion
    }
}
