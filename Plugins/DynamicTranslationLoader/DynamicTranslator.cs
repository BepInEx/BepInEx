using BepInEx;
using BepInEx.Common;
using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicTranslationLoader
{
    public class DynamicTranslator : BaseUnityPlugin
    {
        private static Dictionary<string, string> translations = new Dictionary<string, string>();
        private static List<string> untranslated = new List<string>();

        public override string Name => "Dynamic Translator";

        public DynamicTranslator()
        {
            string[] translation = File.ReadAllLines(Utility.CombinePaths(Utility.PluginsDirectory, "translation", "translation.txt"));

            for (int i = 0; i < translation.Length; i++)
            {
                string line = translation[i];
                if (!line.Contains('='))
                    continue;

                string[] split = line.Split('=');

                translations[split[0]] = split[1];
            }
            
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.dynamictranslationloader");
            

            MethodInfo original = AccessTools.Property(typeof(TMP_Text), "text").GetSetMethod();

            HarmonyMethod prefix = new HarmonyMethod(typeof(Hooks).GetMethod("LabelTextHook"));
            
            harmony.Patch(original, prefix, null);


            original = AccessTools.Method(typeof(TMP_Text), "SetText", new[] { typeof(string) });

            prefix = new HarmonyMethod(typeof(Hooks).GetMethod("SetTextHook"));

            harmony.Patch(original, prefix, null);
        }

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            TranslateAll();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
            {
                Dump();
                Chainloader.Log($"Text dumped to \"{Path.GetFullPath("dumped-tl.txt")}\"", true);
            }
        }

        void TranslateAll()
        {
            foreach (TextMeshProUGUI gameObject in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                //gameObject.text = "Harsh is shit";

                if (translations.ContainsKey(gameObject.text))
                    gameObject.text = translations[gameObject.text];
                else if (!untranslated.Contains(gameObject.text) &&
                        !translations.ContainsValue(gameObject.text.Trim()))
                {
                    untranslated.Add(gameObject.text);
                }
                            
            }
        }

        void Dump()
        {
            string output = "";

            foreach (var kv in translations)
                output += $"{kv.Key.Trim()}={kv.Value.Trim()}\r\n";

            foreach (var text in untranslated)
                if(!Regex.Replace(text, @"[\d-]", string.Empty).IsNullOrWhiteSpace()
                        && !text.Contains("Reset"))
                    output += $"{text.Trim()}=\r\n";

            File.WriteAllText("dumped-tl.txt", output);
        }

        public static string Translate(string input)
        {
            if (translations.ContainsKey(input))
                return translations[input];

            if (!untranslated.Contains(input))
                untranslated.Add(input);

            return input;
        }
    }
}
