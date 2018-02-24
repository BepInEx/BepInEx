using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BepInEx
{
    /// <summary>
    /// The manager and loader for all plugins, and the entry point for BepInEx.
    /// </summary>
    public class Chainloader
    {
        /// <summary>
        /// The loaded and initialized list of plugins.
        /// </summary>
        public static List<BaseUnityPlugin> Plugins { get; protected set; } = new List<BaseUnityPlugin>();

        /// <summary>
        /// The GameObject that all plugins are attached to as components.
        /// </summary>
        public static GameObject ManagerObject { get; protected set; } = new GameObject("BepInEx_Manager");


        static bool loaded = false;

        /// <summary>
        /// The entry point for BepInEx, called on the very first LoadScene() from UnityEngine.
        /// </summary>
        public static void Initialize()
        {
            if (loaded)
                return;

            try
            {
                UnityEngine.Object.DontDestroyOnLoad(ManagerObject);

                if (Directory.Exists(Utility.PluginsDirectory))
                {
                    var pluginTypes = LoadTypes<BaseUnityPlugin>(Utility.PluginsDirectory);

                    //Log($"{pluginTypes.Count()} plugins found");

                    foreach (Type t in pluginTypes)
                    {
                        var plugin = (BaseUnityPlugin)ManagerObject.AddComponent(t);
                        Plugins.Add(plugin);
                        //Log($"Loaded [{plugin.Name}]");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
                //UnityInjector.ConsoleUtil.ConsoleEncoding.ConsoleCodePage = 932;

                Console.WriteLine("Error occurred starting the game");
                Console.WriteLine(ex.ToString());
            }

            loaded = true;
        }

        /// <summary>
        /// Checks all plugins to see if a plugin with a certain ID is loaded.
        /// </summary>
        /// <param name="ID">The ID to check for.</param>
        /// <returns></returns>
        public static bool IsIDLoaded(string ID)
        {
            foreach (var plugin in Plugins)
                if (plugin != null && plugin.enabled && plugin.ID == ID)
                    return true;

            return false;
        }

        /// <summary>
        /// Loads a list of types from a directory containing assemblies, that derive from a base type.
        /// </summary>
        /// <typeparam name="T">The specfiic base type to search for.</typeparam>
        /// <param name="directory">The directory to search for assemblies.</param>
        /// <returns>Returns a list of found derivative types.</returns>
        public static List<Type> LoadTypes<T>(string directory)
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
                catch (BadImageFormatException)
                {

                }
            }

            return types;
        }
    }
}
