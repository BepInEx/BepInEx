using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BepInEx
{
    //Adapted from https://github.com/Eusth/IPA/blob/0df8b1ecb87fdfc9e169365cb4a8fd5a909a2ad6/IllusionInjector/PluginComponent.cs
    public class BepInComponent : MonoBehaviour
    {
        private bool quitting = false;

        public static BepInComponent Create()
        {
            return new GameObject("BepInEx_Manager").AddComponent<BepInComponent>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            Console.WriteLine("Component ready");
        }

        void Update()
        {
            //Console.WriteLine("Update");
        }

        void LateUpdate()
        {
            
        }

        void FixedUpdate()
        {
            
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
