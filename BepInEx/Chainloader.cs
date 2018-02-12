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
        public static IEnumerable<ITranslationPlugin> TLPlugins;

        public static void Initialize()
        {
            if (loaded)
                return;

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            Console.WriteLine("Chainloader started");

            TranslationPlugin translationPlugin = new TranslationPlugin();

            Plugins = new List<IUnityPlugin>
            {
                new DumpScenePlugin(),
                new UnlockedInputPlugin(),
                translationPlugin
            };

            TLPlugins = new List<ITranslationPlugin>
            {
                translationPlugin
            };

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            UnityInjector.ConsoleUtil.ConsoleEncoding.ConsoleCodePage = 932;
            Console.WriteLine($"{Plugins.Count()} plugins loaded");

            
            BepInComponent.Create();

            loaded = true;
        }

        public static string TextLoadedHook(string text)
        {
            foreach (var plugin in TLPlugins)
                if (plugin.TryTranslate(text, out string output))
                    return output;

            return text;
        }
    }
}
