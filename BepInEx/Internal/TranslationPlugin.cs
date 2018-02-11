using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx.Internal
{
    public class TranslationPlugin : IUnityPlugin
    {
        Dictionary<string, string> translations = new Dictionary<string, string>();

        public TranslationPlugin()
        {
            string[] japanese = File.ReadAllLines(Utility.CombinePaths(Utility.ExecutingDirectory, "translation", "japanese.txt"));
            string[] translation = File.ReadAllLines(Utility.CombinePaths(Utility.ExecutingDirectory, "translation", "translated.txt"));

            for (int i = 0; i < japanese.Length; i++)
                translations[japanese[i]] = translation[i];
        }

        public void OnStart()
        {

        }

        public void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            Translate();
        }

        public void OnFixedUpdate()
        {
            
        }

        public void OnLateUpdate()
        {
            
        }

        public void OnUpdate()
        {
            if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f9")))
            {
                Translate();
            }
        }

        void Translate()
        {
            foreach (TextMeshProUGUI gameObject in GameObject.FindObjectsOfType<TextMeshProUGUI>())
            {
                //gameObject.text = "Harsh is shit";

                if (translations.ContainsKey(gameObject.text))
                    gameObject.text = translations[gameObject.text];
            }
        }
    }
}
