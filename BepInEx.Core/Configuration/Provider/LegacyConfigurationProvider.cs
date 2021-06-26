using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx.Configuration
{
    public class LegacyConfigurationProvider : IConfigurationProvider
    {
        public IDictionary<string, string> RawData { get; private set; }

        public void Load(string resourceUri)
        {
            if (!File.Exists(resourceUri))
                throw new FileNotFoundException(resourceUri);

            var dict = new Dictionary<string, string>();
            var currentSection = new string[0];
            
            foreach (var rawLine in File.ReadAllLines(resourceUri))
            {
                var line = rawLine.Trim();
                
                if (line.StartsWith("#")) //comment
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]")) //section
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim().Split('.');
                    continue;
                }

                var split = line.Split(new[] { '=' }, 2); //actual config line
                if (split.Length != 2)
                    continue; //empty/invalid line

                var currentKey = split[0].Trim();
                var currentValue = split[1].Trim();

                dict[ConfigurationPath.Combine(currentSection.Concat(new []{ currentKey }))] = currentValue;
            }

            RawData = dict;
        }

        public void Save(string resourceUri) => throw new System.NotImplementedException();
    }
}
