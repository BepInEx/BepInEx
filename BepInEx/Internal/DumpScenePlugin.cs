using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx.Internal
{
    class DumpScenePlugin : IUnityPlugin
    {
        public void OnStart()
        {

        }

        public void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {

        }

        public void OnFixedUpdate()
        {

        }

        public void OnLateUpdate()
        {

        }

        public void OnUpdate()
        {
            if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f8")))
            {
                //DumpScene();
            }
        }

        static List<string> lines;

        public static void DumpScene()
        {
            lines = new List<string>();

            string filename = @"M:\unity-scene.txt";

            Debug.Log("Dumping scene to " + filename + " ...");
            using (StreamWriter writer = new StreamWriter(filename, false))
            {
                foreach (GameObject gameObject in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (gameObject.activeInHierarchy)
                        DumpGameObject(gameObject, writer, "");

                }

                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
            }
            Debug.Log("Scene dumped to " + filename);
        }

        private static void DumpGameObject(GameObject gameObject, StreamWriter writer, string indent)
        {
            //writer.WriteLine("{0}+{1}+{2}", indent, gameObject.name, gameObject.GetType().FullName);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                DumpComponent(component, writer, indent + "  ");
            }

            foreach (Transform child in gameObject.transform)
            {
                DumpGameObject(child.gameObject, writer, indent + "  ");
            }
        }

        private static void DumpComponent(Component component, StreamWriter writer, string indent)
        {
            //writer.WriteLine("{0}{1}", indent, (component == null ? "(null)" : component.GetType().FullName));
            if (component is TextMeshProUGUI)
            {
                string text = ((TextMeshProUGUI)component).text;

                if (!text.IsNullOrWhiteSpace()
                    && !text.Contains("Reset")
                    && !Regex.Replace(text, @"[\d-]", string.Empty).IsNullOrWhiteSpace())
                {
                    if (!lines.Contains(text))
                        lines.Add(text);
                }
            }

        }
    }
}
