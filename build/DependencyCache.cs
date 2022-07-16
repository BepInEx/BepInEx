using System;
using System.Collections.Generic;
using System.IO;
using Nuke.Common.IO;
using static Nuke.Common.IO.SerializationTasks;

readonly record struct DependencyCache(AbsolutePath CacheFile)
{
    readonly IDictionary<string, string> cache =
        CacheFile.Exists()
            ? JsonDeserialize<Dictionary<string, string>>(File.ReadAllText(CacheFile))
            : new Dictionary<string, string>();

    public void Refresh(string name, string key, Action process)
    {
        if (cache.TryGetValue(name, out var curKey) && curKey == key) return;
        process();
        cache[name] = key;
    }

    public void Save() => File.WriteAllText(CacheFile, JsonSerialize(cache));
}
