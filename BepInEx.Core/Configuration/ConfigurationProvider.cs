using System.Collections.Generic;

namespace BepInEx.Configuration
{
    public abstract class ConfigurationProvider
    {
        public IDictionary<string, string> RawData { get; }

        public abstract void Load();

        public abstract void Save();
    }
}
