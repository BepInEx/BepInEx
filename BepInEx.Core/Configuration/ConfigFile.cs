using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;

// TODO: Check if IDictionary implementation is reasonable

namespace BepInEx.Configuration
{
    /// <summary>
    ///     A helper class to handle persistent data. All public methods are thread-safe.
    /// </summary>
    public class ConfigFile : IDictionary<ConfigDefinition, ConfigEntryBase>
    {
        public static readonly char PathSeparator = '.';
        
        private readonly BepInPlugin _ownerMetadata;

        /// <inheritdoc />
        public ConfigFile(string configPath, bool saveOnInit, BepInPlugin ownerMetadata = null) :
            this(InitDefaultProvider(configPath), saveOnInit, ownerMetadata) { }

        /// <summary>
        ///     Create a new config file at the specified config path.
        /// </summary>
        /// <param name="configPath">Full path to a file that contains settings. The file will be created as needed.</param>
        /// <param name="saveOnInit">If the config file/directory doesn't exist, create it immediately.</param>
        /// <param name="ownerMetadata">Information about the plugin that owns this setting file.</param>
        public ConfigFile(IConfigurationProvider configurationProvider, bool saveOnInit, BepInPlugin ownerMetadata)
        {
            _ownerMetadata = ownerMetadata;
            ConfigurationProvider = configurationProvider;

            Reload();
            if (saveOnInit) Save();
        }

        public static ConfigFile CoreConfig { get; } = new(Paths.BepInExConfigPath, true);

        public IConfigurationProvider ConfigurationProvider { get; }

        /// <summary>
        ///     All config entries inside
        /// </summary>
        protected Dictionary<ConfigDefinition, ConfigEntryBase> Entries { get; } = new();

        private Dictionary<ConfigDefinition, string> OrphanedEntries { get; } = new();

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
                {
                    return Entries[key];
                }
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
            {
                Entries.Add(item.Key, item.Value);
            }
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<ConfigDefinition, ConfigEntryBase> item)
        {
            lock (_ioLock)
            {
                return ((ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>) Entries).Contains(item);
            }
        }

        void ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>.CopyTo(
            KeyValuePair<ConfigDefinition, ConfigEntryBase>[] array,
            int arrayIndex)
        {
            lock (_ioLock)
            {
                ((ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>) Entries).CopyTo(array, arrayIndex);
            }
        }

        bool ICollection<KeyValuePair<ConfigDefinition, ConfigEntryBase>>.Remove(
            KeyValuePair<ConfigDefinition, ConfigEntryBase> item)
        {
            lock (_ioLock)
            {
                return Entries.Remove(item.Key);
            }
        }

        /// <inheritdoc />
        public int Count
        {
            get
            {
                lock (_ioLock)
                {
                    return Entries.Count;
                }
            }
        }

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public bool ContainsKey(ConfigDefinition key)
        {
            lock (_ioLock)
            {
                return Entries.ContainsKey(key);
            }
        }

        /// <inheritdoc />
        public void Add(ConfigDefinition key, ConfigEntryBase value) =>
            throw new InvalidOperationException("Directly adding a config entry is not supported");

        /// <inheritdoc />
        public bool Remove(ConfigDefinition key)
        {
            lock (_ioLock)
            {
                return Entries.Remove(key);
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_ioLock)
            {
                Entries.Clear();
            }
        }

        bool IDictionary<ConfigDefinition, ConfigEntryBase>.TryGetValue(ConfigDefinition key, out ConfigEntryBase value)
        {
            lock (_ioLock)
            {
                return Entries.TryGetValue(key, out value);
            }
        }

        /// <inheritdoc />
        ConfigEntryBase IDictionary<ConfigDefinition, ConfigEntryBase>.this[ConfigDefinition key]
        {
            get
            {
                lock (_ioLock)
                {
                    return Entries[key];
                }
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
                {
                    return Entries.Keys.ToArray();
                }
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
                {
                    return Entries.Values.ToArray();
                }
            }
        }

        private static IConfigurationProvider InitDefaultProvider(string configPath) =>
            // TODO: If .cfg => use legacy provider
            // TODO: If .toml => use TOML provider
            new LegacyConfigurationProvider(configPath);

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
                ConfigurationProvider.Load();
            }
            
            // TODO: Implement configuration document to document entry parsing

            OnConfigReloaded();
        }

        /// <summary>
        ///     Writes the config to disk.
        /// </summary>
        public void Save()
        {
            lock (_ioLock)
            {
                if (_ownerMetadata != null)
                    ConfigurationProvider.Set(null, new ConfigurationNode
                    {
                        Comment = new StringBuilder()
                                  .AppendLine($"Settings file was created by plugin {_ownerMetadata.Name} v{_ownerMetadata.Version}")
                                  .AppendLine($"Plugin GUID: {_ownerMetadata.GUID}")
                                  .ToString()
                    });

                ConfigurationProvider.Save();
            }
        }

        #endregion

        #region Wraps

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
            lock (_ioLock)
            {
                if (Entries.TryGetValue(configDefinition, out var rawEntry))
                    return (ConfigEntry<T>) rawEntry;

                var entry = new ConfigEntry<T>(this, configDefinition, defaultValue, configDescription);
                Entries[configDefinition] = entry;

                entry.BoxedValue = ConfigurationProvider.GetValue(configDefinition.ConfigPath, typeof(T)) ?? defaultValue;

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

            // TODO: Set value to provider
            
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
