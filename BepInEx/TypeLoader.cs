using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BepInEx
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
                        if (type.IsInterface || type.IsAbstract)
                        {
                            continue;
                        }
                        else
                        {
                            if (type.BaseType == pluginType)
                                types.Add(type);
                        }
                    }
                }
                catch (BadImageFormatException) { } //unmanaged DLL
                catch (ReflectionTypeLoadException)
                {
                    BepInLogger.Log($"ERROR! Could not load \"{Path.GetFileName(dll)}\" as a plugin!");
                } 
            }

            return types;
        }

        public static BepInPlugin GetMetadata(object plugin)
        {
            return GetMetadata(plugin.GetType());
        }

        public static BepInPlugin GetMetadata(Type pluginType)
        {
            object[] attributes = pluginType.GetCustomAttributes(typeof(BepInPlugin), false);

            if (attributes.Length == 0)
                return null;

            return (BepInPlugin)attributes[0];
        }

        public static IEnumerable<T> GetAttributes<T>(object plugin) where T : Attribute
        {
            return GetAttributes<T>(plugin.GetType());
        }

        public static IEnumerable<T> GetAttributes<T>(Type pluginType) where T : Attribute
        {
            return pluginType.GetCustomAttributes(typeof(T), true).Cast<T>();
        }

        public static IEnumerable<Type> GetDependencies(Type Plugin, IEnumerable<Type> AllPlugins)
        {
            object[] attributes = Plugin.GetCustomAttributes(typeof(BepInDependency), true);

            List<Type> dependencyTypes = new List<Type>();

            foreach (BepInDependency dependency in attributes)
            {
                Type dependencyType = AllPlugins.FirstOrDefault(x => GetMetadata(x)?.GUID == dependency.DependencyGUID);

                if (dependencyType == null)
                {
                    if ((dependency.Flags & BepInDependency.DependencyFlags.SoftDependency) != 0)
                        continue; //skip on soft dependencies

                    throw new MissingDependencyException("Cannot find dependency type.");
                }
                    

                dependencyTypes.Add(dependencyType);
            }

            return dependencyTypes;
        }
    }

    public class MissingDependencyException : Exception
    {
        public MissingDependencyException() : base()
        {

        }

        public MissingDependencyException(string message) : base(message)
        {

        }
    }
}
