using System;
using System.Collections.Generic;
using System.IO;
using Cake.Core.IO;
using Cake.Json;

readonly record struct DependencyCache(BuildContext Ctx, FilePath CacheFile)
{
    readonly IDictionary<string, string> cache =
        File.Exists(CacheFile.FullPath)
            ? Ctx.DeserializeJsonFromFile<Dictionary<string, string>>(CacheFile.FullPath)
            : new Dictionary<string, string>();

    public void Refresh(string name, string key, Action process)
    {
        if (cache.TryGetValue(name, out var curKey) && curKey == key) return;
        process();
        cache[name] = key;
    }

    public void Save() => Ctx.SerializeJsonToPrettyFile(CacheFile.FullPath, cache);
}
