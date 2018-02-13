using BepInEx;
using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicTranslationLoader
{
    public class DynamicTranslator : BaseUnityPlugin
    {
        Dictionary<string, string> translations = new Dictionary<string, string>();
        List<string> untranslated = new List<string>();

        public override string Name => "Dynamic Translator";

        public DynamicTranslator()
        {
            string[] translation = File.ReadAllLines(Utility.CombinePaths(Utility.ExecutingDirectory, "translation", "translation.txt"));

            for (int i = 0; i < translation.Length; i++)
            {
                string line = translation[i];
                if (!line.Contains('='))
                    continue;

                string[] split = line.Split('=');

                translations[split[0]] = split[1];
            }
        }

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            Translate();
        }

        void OnUpdate()
        {
            if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f9")))
            {
                Translate();
            }
            else if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f10")))
            {
                Dump();
                Console.WriteLine($"Text dumped to \"{Path.GetFullPath("dumped-tl.txt")}\"");
            }
        }

        void Translate()
        {
            foreach (TextMeshProUGUI gameObject in GameObject.FindObjectsOfType<TextMeshProUGUI>())
            {
                //gameObject.text = "Harsh is shit";

                if (translations.ContainsKey(gameObject.text))
                    gameObject.text = translations[gameObject.text];
                else
                    if (!untranslated.Contains(gameObject.text))
                    untranslated.Add(gameObject.text);
            }
        }

        void Dump()
        {
            string output = "";

            foreach (var kv in translations)
                output += $"{kv.Key.Trim()}={kv.Value.Trim()}\r\n";

            foreach (var text in untranslated)
                if (!text.IsNullOrWhiteSpace()
                    && !text.Contains("Reset")
                    && !Regex.Replace(text, @"[\d-]", string.Empty).IsNullOrWhiteSpace()
                    && !translations.ContainsValue(text.Trim()))
                    output += $"{text.Trim()}=\r\n";

            File.WriteAllText("dumped-tl.txt", output);
        }

        public bool TryTranslate(string input, out string output)
        {
            if (translations.ContainsKey(input))
            {
                output = translations[input];
                return true;
            }
            output = null;

            if (!untranslated.Contains(input))
                untranslated.Add(input);

            return false;
        }
    }
}
