using BepInEx.Common;
using ChaCustom;
using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BepInEx
{
    public class Chainloader
    {
        static bool loaded = false;
        public static IEnumerable<Type> Plugins { get; protected set; }
        public static GameObject ManagerObject { get; protected set; }

        public static void Initialize()
        {
            if (loaded)
                return;

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            Console.WriteLine("Chainloader started");

            if (Directory.Exists(Utility.PluginsDirectory))
            {
                Plugins = LoadTypes<BaseUnityPlugin>(Utility.PluginsDirectory);

                //UnityInjector.ConsoleUtil.ConsoleEncoding.ConsoleCodePage = 932;
                Console.WriteLine($"{Plugins.Count()} plugins loaded");
            }
            else
            {
                Plugins = new List<Type>();
                Console.WriteLine("Plugins directory not found, skipping");
            }
            
            
            ManagerObject = BepInComponent.Create();

            loaded = true;
        }

        public static ICollection<Type> LoadTypes<T>(string directory)
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
