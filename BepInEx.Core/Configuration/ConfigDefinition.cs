using System;
using System.Linq;
using HarmonyLib;

namespace BepInEx.Configuration
{
    /// <summary>
    ///     Section and key of a setting. Used as a unique key for identification within a
    ///     <see cref="T:BepInEx.Configuration.ConfigFile" />.
    ///     The same definition can be used in multiple config files, it will point to different settings then.
    /// </summary>
    /// <inheritdoc />
    public class ConfigDefinition : IEquatable<ConfigDefinition>
    {
        private static readonly char[] _invalidConfigChars = { '=', '\n', '\t', '\\', '"', '\'', '[', ']', ConfigFile.PathSeparator };

        /// <summary>
        ///     Create a new definition. Definitions with same section and key are equal.
        /// </summary>
        /// <param name="section">Group of the setting, case sensitive.</param>
        /// <param name="key">Name of the setting, case sensitive.</param>
        public ConfigDefinition(string section, string key)
        {
            CheckInvalidConfigChars(section, nameof(section));
            CheckInvalidConfigChars(key, nameof(key));
            Key = key;
            Section = section;
        }

        public ConfigDefinition(string fullPath) : this(fullPath.Split(ConfigFile.PathSeparator))
        {
        }
        
        public ConfigDefinition(params string[] pathComponents)
        {
            foreach (var pathComponent in pathComponents)
                CheckInvalidConfigChars(pathComponent, nameof(pathComponents));
            Key = string.Join(ConfigFile.PathSeparator.ToString(), pathComponents.Take(pathComponents.Length - 1).ToArray());
            Section = pathComponents[^1];
        }

        /// <summary>
        ///     Group of the setting. All settings within a config file are grouped by this.
        /// </summary>
        public string Section { get; }

        /// <summary>
        ///     Name of the setting.
        /// </summary>
        public string Key { get; }
        
        internal string[] ConfigPath => Section.Split(ConfigFile.PathSeparator).AddItem(Key).ToArray();

        /// <summary>
        ///     Check if the definitions are the same.
        /// </summary>
        /// <inheritdoc />
        public bool Equals(ConfigDefinition other)
        {
            if (other == null) return false;
            return string.Equals(Key, other.Key)
                && string.Equals(Section, other.Section);
        }

        private static void CheckInvalidConfigChars(string val, string name)
        {
            if (val == null) throw new ArgumentNullException(name);
            if (val.Any(c => _invalidConfigChars.Contains(c)))
                throw new
                    ArgumentException($"Cannot use any of the following characters in section and key names: {string.Join(" ", _invalidConfigChars.Select(c => c.ToString().Escape()).ToArray())}",
                                      name);
        }

        /// <summary>
        ///     Check if the definitions are the same.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;

            return Equals(obj as ConfigDefinition);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Key != null ? Key.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Section != null ? Section.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <summary>
        ///     Check if the definitions are the same.
        /// </summary>
        public static bool operator ==(ConfigDefinition left, ConfigDefinition right) => Equals(left, right);

        /// <summary>
        ///     Check if the definitions are the same.
        /// </summary>
        public static bool operator !=(ConfigDefinition left, ConfigDefinition right) => !Equals(left, right);

        /// <inheritdoc />
        public override string ToString() => Section + "." + Key;
    }
}
