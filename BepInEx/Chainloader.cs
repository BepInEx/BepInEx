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
        public static List<BaseUnityPlugin> Plugins { get; protected set; } = new List<BaseUnityPlugin>();
        public static GameObject ManagerObject { get; protected set; } = new GameObject("BepInEx_Manager");



        public delegate void EntryLoggedEventHandler(string entry, bool show = false);

        public static event EntryLoggedEventHandler EntryLogged;


        public static void Log(string entry, bool show = false)
        {
            EntryLogged?.Invoke(entry, show);
        }


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
