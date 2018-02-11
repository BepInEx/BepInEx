using BepInEx.Internal;
using ChaCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public class Chainloader
    {
        static bool loaded = false;
        public static IEnumerable<IUnityPlugin> Plugins;

        public static void Initialize()
        {
            if (loaded)
                return;

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            Console.WriteLine("Chainloader started");

            List<IUnityPlugin> plugins = new List<IUnityPlugin>();
            plugins.Add(new DumpScenePlugin());
            plugins.Add(new TranslationPlugin());
            plugins.Add(new UnlockedInputPlugin());

            Plugins = plugins;

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            Console.WriteLine($"{plugins.Count} plugins loaded");

            
            BepInComponent.Create();

            loaded = true;
        }
    }
}
