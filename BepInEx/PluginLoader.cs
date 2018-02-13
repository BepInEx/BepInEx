using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BepInEx
{
    public static class PluginLoader
    {
        public static ICollection<T> LoadPlugins<T>(string directory)
        {
            List<T> plugins = new List<T>();
            Type pluginType = typeof(T);

            foreach (string dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll"))
            {
                try
                {
                    AssemblyName an = AssemblyName.GetAssemblyName(dll);
                    Assembly assembly = Assembly.Load(an);
                    
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.IsInterface || type.IsAbstract)
                        {
                            continue;
                        }
                        else
                        {
                            if (type.GetInterface(pluginType.FullName) != null)
                            {
                                plugins.Add((T)Activator.CreateInstance(type));
                            }
                        }
                    }
                }
                catch (BadImageFormatException ex)
                {

                }
            }

            return plugins;
        }
    }
}
