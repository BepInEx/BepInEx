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

        private static FieldInfo akf_dictInfo = (typeof(AnimationKeyInfo).GetField("dictInfo", BindingFlags.NonPublic | BindingFlags.Instance));


        private static FieldInfo akf_sliderR = (typeof(PickerSlider).GetField("sliderR", BindingFlags.NonPublic | BindingFlags.Instance));
        private static FieldInfo akf_sliderG = (typeof(PickerSlider).GetField("sliderG", BindingFlags.NonPublic | BindingFlags.Instance));
        private static FieldInfo akf_sliderB = (typeof(PickerSlider).GetField("sliderB", BindingFlags.NonPublic | BindingFlags.Instance));
        private static FieldInfo akf_sliderA = (typeof(PickerSlider).GetField("sliderA", BindingFlags.NonPublic | BindingFlags.Instance));

        public SliderUnlocker()
        {
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.sliderunlocker");

            MethodInfo original = AccessTools.Method(typeof(CustomBase), "ConvertTextFromRate");

            HarmonyMethod postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("ConvertTextFromRateHook"));
            
            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(CustomBase), "ConvertRateFromText");

            postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("ConvertRateFromTextHook"));

            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(Mathf), "Clamp", new Type[] { typeof(float), typeof(float), typeof(float) });
            
            postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("MathfClampHook"));

            harmony.Patch(original, null, postfix);



            original = typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name.Contains("GetInfo")).ToArray()[1];
            
            var prefix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("GetInfoPreHook"));

            postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("GetInfoPostHook"));

            harmony.Patch(original, prefix, postfix);
        }

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (Slider gameObject in GameObject.FindObjectsOfType<Slider>())
            {
                gameObject.maxValue = 2f;
            }

            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.name == "PickerSliderColor" ||
                    gameObject.name == "menuSlider")
                {
                    foreach (Slider slider in gameObject.GetComponents<Slider>())
                    {
                        slider.maxValue = 1f;
                    }
                }
            }

            foreach (PickerSlider gameObject in GameObject.FindObjectsOfType<PickerSlider>())
            {
                ((Slider)akf_sliderA.GetValue(gameObject)).maxValue = 1f;
                ((Slider)akf_sliderR.GetValue(gameObject)).maxValue = 1f;
                ((Slider)akf_sliderG.GetValue(gameObject)).maxValue = 1f;
                ((Slider)akf_sliderB.GetValue(gameObject)).maxValue = 1f;
            }
        }


        [HarmonyPostfix]
        public static void ConvertTextFromRateHook(ref string __result, int min, int max, float value)
        {
            if (min == 0 && max == 100)
                __result = Math.Round(100 * value).ToString();
        }

        [HarmonyPostfix]
        public static void ConvertRateFromTextHook(ref float __result, int min, int max, string buf)
        {
            if (min == 0 && max == 100)
            {
                if (buf.IsNullOrEmpty())
                {
                    __result = 0f;
                }
                else
                {
                    if (!float.TryParse(buf, out float val))
                    {
                        __result = 0f;
                    }
                    else
                    {
                        __result = val / 100;
                    }
                }
            }
        }

        [HarmonyPostfix]
        public static void MathfClampHook(ref float __result, float value, float min, float max)
        {
            if (min == 0f && max == 100f)
                __result = value;
        }

        [HarmonyPrefix]
        public static void GetInfoPreHook(ref float __state, string name, ref float rate, ref Vector3[] value, bool[] flag)
        {
            __state = rate;

            if (rate > 1)
                rate = 1f;
        }

        [HarmonyPostfix]
        public static void GetInfoPostHook(AnimationKeyInfo __instance, bool __result, float __state, string name, float rate, ref Vector3[] value, bool[] flag)
        {
            if (!__result)
                return;
            
            rate = __state;

            if (rate < 0f || rate > 1f)
            {
                var dictInfo = (Dictionary<string, List<AnimationKeyInfo.AnmKeyInfo>>)akf_dictInfo.GetValue(__instance);

                List<AnimationKeyInfo.AnmKeyInfo> list = dictInfo[name];


                if (flag[2])
                {
                    Vector3 min = list[0].scl;
                    Vector3 max = list[list.Count - 1].scl;

                    value[2] = new Vector3(min.x + ((max.x - min.x) * rate),
                                            min.y + ((max.y - min.y) * rate),
                                            min.z + ((max.z - min.z) * rate));
                }
            }
        }
    }
}