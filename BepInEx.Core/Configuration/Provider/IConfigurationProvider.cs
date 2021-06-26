using System.Collections.Generic;

namespace BepInEx.Configuration
{
    public interface IConfigurationProvider
    {
        public IDictionary<string, string> RawData { get; }

        public void Load(string resourceUri);

        public void Save(string resourceUri);
    }
}
