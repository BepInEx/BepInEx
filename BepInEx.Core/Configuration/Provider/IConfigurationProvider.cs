using System;
using System.Collections.Generic;

namespace BepInEx.Configuration
{
    public interface IConfigurationProvider
    {
        void Load();

        void Save();

        ConfigurationNode Get(string[] path);

        void Set(string[] path, ConfigurationNode node);

        ConfigurationNode Delete(string[] path);
        
        IEnumerable<string[]> EntryPaths { get; }
    }

    public class ConfigurationNode
    {
        public string Value { get; init; }
        public string Comment { get; init; }
        public Type ValueType { get; init; }
    }
}
