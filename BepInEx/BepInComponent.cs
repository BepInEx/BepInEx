using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx
{
    //Adapted from https://github.com/Eusth/IPA/blob/0df8b1ecb87fdfc9e169365cb4a8fd5a909a2ad6/IllusionInjector/PluginComponent.cs
    public class BepInComponent : MonoBehaviour
    {
        List<BaseUnityPlugin> Plugins;
        private bool quitting = false;

        public static GameObject Create()
        {
            var obj = new GameObject("BepInEx_Manager");

            var manager = obj.AddComponent<BepInComponent>();

            manager.Plugins = new List<BaseUnityPlugin>();

            foreach (Type t in Chainloader.Plugins)
                manager.Plugins.Add((BaseUnityPlugin)obj.AddComponent(t));

            return obj;
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            Console.WriteLine("Component ready");
        }

        void OnDestroy()
        {
            if (!quitting)
            {
                Create();
            }
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }
    }
}
