using BepInEx;
using ChaCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SliderUnlocker
{
    public class SliderUnlocker : BaseUnityPlugin
    {
        public override string ID => "com.bepis.bepinex.sliderunlocker";
        public override string Name => "Slider Unlocker";
        public override Version Version => new Version("1.5");

        public static float Minimum = -1.0f;
        public static float Maximum = 2.0f;

        void Awake()
        {
            Hooks.InstallHooks();
        }

        void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            SetAllSliders(scene, Minimum, Maximum);
        }


        public void SetAllSliders(Scene scene, float minimum, float maximum)
        {
            List<object> cvsInstances = new List<object>();

            Assembly illusion = typeof(CvsAccessory).Assembly;

            foreach (Type type in illusion.GetTypes())
            {
                if (type.Name.ToUpper().StartsWith("CVS") &&
                    type != typeof(CvsDrawCtrl) &&
                    type != typeof(CvsColor))
                {
                    cvsInstances.AddRange(GameObject.FindObjectsOfType(type));

                    foreach(GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        cvsInstances.AddRange(gameObject.GetComponents(type));
                    }
                }

            }
                
            foreach (object cvs in cvsInstances)
            {
                if (cvs == null)
                    continue;

                var fields = cvs.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                    .Where(x => typeof(Slider).IsAssignableFrom(x.FieldType));

                foreach (FieldInfo field in fields)
                {
                    if (field.Name == "sldGlossPow")
                        continue;

                    Slider slider = (Slider)field.GetValue(cvs);

                    if (slider == null)
                        continue;

                    slider.minValue = minimum;
                    slider.maxValue = maximum;
                }
            }
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
        #endregion
    }
}