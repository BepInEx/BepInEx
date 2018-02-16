using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorCorrector
{
    public class ColorCorrector : BaseUnityPlugin
    {
        public override string Name => "Color Corrector";
        
        AmplifyColorEffect component;

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            DisableEffects();
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
            component.enabled = !component.enabled;
        }

        void DisableEffects()
        {
            if (Camera.main != null && Camera.main?.gameObject != null)
            {
                var c = Camera.main.gameObject.GetComponent<AmplifyColorEffect>();
                if (c != null)
                {
                    component = c;
                    component.enabled = false;
                }
            }
        }
    }
}
