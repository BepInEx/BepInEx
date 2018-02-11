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
        IEnumerable<IUnityPlugin> Plugins;
        private bool quitting = false;

        public static BepInComponent Create()
        {
            return new GameObject("BepInEx_Manager").AddComponent<BepInComponent>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            Plugins = Chainloader.Plugins;
        }

        void Start()
        {
            Console.WriteLine("Component ready");

            foreach (IUnityPlugin plugin in Plugins)
                plugin.OnStart();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += LevelFinishedLoading;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= LevelFinishedLoading;
        }

        void Update()
        {
            foreach (IUnityPlugin plugin in Plugins)
                plugin.OnUpdate();
        }

        void LateUpdate()
        {
            foreach (IUnityPlugin plugin in Plugins)
                plugin.OnLateUpdate();
        }

        void FixedUpdate()
        {
            foreach (IUnityPlugin plugin in Plugins)
                plugin.OnFixedUpdate();
        }

        void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (IUnityPlugin plugin in Plugins)
                plugin.OnLevelFinishedLoading(scene, mode);
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
