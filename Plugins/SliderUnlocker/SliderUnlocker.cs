using BepInEx;
using ChaCustom;
using Harmony;
using Illusion.Component.UI.ColorPicker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SliderUnlocker
{
    public class SliderUnlocker : BaseUnityPlugin
    {
        public override string Name => "Slider Unlocker";

        public static float Minimum = -1.0f;
        public static float Maximum = 2.0f;

        public SliderUnlocker()
        {
            PatchMethods();
        }

        private void PatchMethods()
        {
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.sliderunlocker");

            MethodInfo original = AccessTools.Method(typeof(CustomBase), "ConvertTextFromRate");

            HarmonyMethod postfix = new HarmonyMethod(typeof(Hooks).GetMethod("ConvertTextFromRateHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(CustomBase), "ConvertRateFromText");

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("ConvertRateFromTextHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(Mathf), "Clamp", new Type[] { typeof(float), typeof(float), typeof(float) });

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("MathfClampHook"));

            harmony.Patch(original, null, postfix);




            original = typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name.Contains("GetInfo")).ToArray()[0];

            var prefix = new HarmonyMethod(typeof(Hooks).GetMethod("GetInfoSingularPreHook"));

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("GetInfoSingularPostHook"));

            harmony.Patch(original, prefix, postfix);



            original = typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name.Contains("GetInfo")).ToArray()[1];

            prefix = new HarmonyMethod(typeof(Hooks).GetMethod("GetInfoPreHook"));

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("GetInfoPostHook"));

            harmony.Patch(original, prefix, postfix);
        }

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
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
                    type.Name != "CvsDrawCtrl")
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

                foreach (Slider slider in fields.Select(x => x.GetValue(cvs)))
                {
                    if (slider == null)
                        continue;

                    slider.minValue = minimum;
                    slider.maxValue = maximum;
                }
            }
        }
    }
}