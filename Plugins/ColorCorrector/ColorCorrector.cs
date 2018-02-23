using BepInEx;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;

namespace ColorCorrector
{
    public class ColorCorrector : BaseUnityPlugin
    {
        public override string ID => "colorcorrector";
        public override string Name => "Color Filter Remover";
        public override Version Version => new Version("1.2");

        #region Config properties
        private bool SaturationEnabled
        {
            get => bool.Parse(BepInEx.Config.GetEntry("colorcorrector-saturationenabled", "True"));
            set => BepInEx.Config.SetEntry("colorcorrector-saturationenabled", value.ToString());
        }

        private bool BloomEnabled
        {
            get => bool.Parse(BepInEx.Config.GetEntry("colorcorrector-bloomenabled", "True"));
            set => BepInEx.Config.SetEntry("colorcorrector-bloomenabled", value.ToString());
        }
        #endregion

        AmplifyColorEffect amplifyComponent;
        BloomAndFlares bloomComponent;

        protected void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            if (Camera.main != null && Camera.main?.gameObject != null)
            {
                amplifyComponent = Camera.main.gameObject.GetComponent<AmplifyColorEffect>();
                bloomComponent = Camera.main.gameObject.GetComponent<BloomAndFlares>();

                SetEffects(SaturationEnabled, BloomEnabled);
            }
        }

        void SetEffects(bool satEnabled, bool bloomEnabled)
        {
            if (amplifyComponent != null)
                amplifyComponent.enabled = satEnabled;

            if (bloomComponent != null)
                bloomComponent.enabled = bloomEnabled;
        }

        #region MonoBehaviour
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
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F6))
            {
                showingUI = !showingUI;
            }
        }
        #endregion

        #region UI 
        private Rect UI = new Rect(20, 20, 200, 100);
        private bool showingUI = false;

        void OnGUI()
        {
            if (showingUI)
                UI = GUI.Window(Name.GetHashCode() + 0, UI, WindowFunction, "Filter settings");
        }

        void WindowFunction(int windowID)
        {
            bool satEnabled = GUI.Toggle(new Rect(10, 20, 180, 20), SaturationEnabled, " Saturation filter enabled");
            bool bloomEnabled = GUI.Toggle(new Rect(10, 40, 180, 20), BloomEnabled, " Bloom filter enabled");

            if (GUI.changed)
            {
                SaturationEnabled = satEnabled;
                BloomEnabled = bloomEnabled;

                SetEffects(satEnabled, bloomEnabled);
            }

            GUI.DragWindow();
        }
        #endregion
    }
}
