using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace BepInEx.Bootstrap
{
    public static class TypeLoader
    {
        /// <summary>
        /// Loads a list of types from a directory containing assemblies, that derive from a base type.
        /// </summary>
        /// <typeparam name="T">The specfiic base type to search for.</typeparam>
        /// <param name="directory">The directory to search for assemblies.</param>
        /// <returns>Returns a list of found derivative types.</returns>
        public static IEnumerable<Type> LoadTypes<T>(string directory)
        {
            List<Type> types = new List<Type>();
            Type pluginType = typeof(T);

            foreach (string dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll"))
            {
                try
                {
                    AssemblyName an = AssemblyName.GetAssemblyName(dll);
                    Assembly assembly = Assembly.Load(an);

                    foreach (Type type in assembly.GetTypes())
                    {
                        if (!type.IsInterface && !type.IsAbstract && type.BaseType == pluginType)
                            types.Add(type);
                    }
                }
                catch (BadImageFormatException) { } //unmanaged DLL
                catch (ReflectionTypeLoadException)
                {
                    Logger.Log(LogLevel.Error, $"Could not load \"{Path.GetFileName(dll)}\" as a plugin!");
                }
            }

            return types;
        }
    }
}
