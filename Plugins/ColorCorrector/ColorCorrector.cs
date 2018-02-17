using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;

namespace ColorCorrector
{
    public class ColorCorrector : BaseUnityPlugin
    {
        public override string Name => "Color Filter Remover";
        
        AmplifyColorEffect amplifyComponent;
        BloomAndFlares bloomComponent;

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            if (Camera.main != null && Camera.main?.gameObject != null)
            {
                amplifyComponent = Camera.main.gameObject.GetComponent<AmplifyColorEffect>(); ;
                bloomComponent = Camera.main.gameObject.GetComponent<BloomAndFlares>();
            }
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F6))
            {
                ToggleEffects();
            }
        }

        void ToggleEffects()
        {
            amplifyComponent.enabled = !amplifyComponent.enabled;
            bloomComponent.enabled = !bloomComponent.enabled;
            Console.WriteLine($"Amplify Filter Enabled: {amplifyComponent.enabled}");
            Console.WriteLine($"Bloom Filter Enabled: {bloomComponent.enabled}");
        }
    }
}
