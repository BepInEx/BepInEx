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

        private bool CorrectorEnabled
        {
            get => bool.Parse(BepInEx.Config.GetEntry("colorcorrector-enabled", "False"));
            set => BepInEx.Config.SetEntry("colorcorrector-enabled", value.ToString());
        }
        
        AmplifyColorEffect amplifyComponent;
        BloomAndFlares bloomComponent;

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            if (Camera.main != null && Camera.main?.gameObject != null)
            {
                amplifyComponent = Camera.main.gameObject.GetComponent<AmplifyColorEffect>();
                bloomComponent = Camera.main.gameObject.GetComponent<BloomAndFlares>();

                SetEffects(!CorrectorEnabled);
            }
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F6))
            {
                CorrectorEnabled = !CorrectorEnabled;
                SetEffects(!CorrectorEnabled);
            }
        }

        void SetEffects(bool filterEnabled)
        {
            amplifyComponent.enabled = filterEnabled;
            bloomComponent.enabled = filterEnabled;
            Console.WriteLine($"Amplify Filter Enabled: {filterEnabled}");
            Console.WriteLine($"Bloom Filter Enabled: {filterEnabled}");
        }
    }
}
