using ChaCustom;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SliderUnlocker
{
    public static class Hooks
    {
        public static void InstallHooks()
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

        private static FieldInfo akf_dictInfo = (typeof(AnimationKeyInfo).GetField("dictInfo", BindingFlags.NonPublic | BindingFlags.Instance));

        public static void ConvertTextFromRateHook(ref string __result, int min, int max, float value)
        {
            if (min == 0 && max == 100)
                __result = Math.Round(100 * value).ToString();
        }

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

        public static void MathfClampHook(ref float __result, float value, float min, float max)
        {
            if (min == 0f && max == 100f)
                __result = value;
        }

        public static void GetInfoPreHook(ref float __state, string name, ref float rate, ref Vector3[] value, bool[] flag)
        {
            __state = rate;

            if (rate > 1)
                rate = 1f;


            if (rate < 0)
                rate = 0f;
        }

        public static void GetInfoSingularPreHook(ref float __state, string name, ref float rate, ref Vector3 value, byte type)
        {
            __state = rate;

            if (rate > 1)
                rate = 1f;


            if (rate < 0)
                rate = 0f;
        }

        public static void GetInfoSingularPostHook(AnimationKeyInfo __instance, bool __result, float __state, string name, float rate, ref Vector3 value, byte type)
        {
            if (!__result)
                return;

            rate = __state;

            if (rate < 0f || rate > 1f)
            {
                var dictInfo = (Dictionary<string, List<AnimationKeyInfo.AnmKeyInfo>>)akf_dictInfo.GetValue(__instance);

                List<AnimationKeyInfo.AnmKeyInfo> list = dictInfo[name];

                switch (type)
                {
                    case 0:
                        value = SliderMath.CalculatePosition(list, rate);
                        break;
                    case 1:
                        value = SliderMath.SafeCalculateRotation(value, name, list, rate);
                        break;
                    default:
                        value = SliderMath.CalculateScale(list, rate);
                        break;
                }
            }
        }

        public static void GetInfoPostHook(AnimationKeyInfo __instance, bool __result, float __state, string name, float rate, ref Vector3[] value, bool[] flag)
        {
            if (!__result)
                return;

            rate = __state;

            if (rate < 0f || rate > 1f)
            {
                var dictInfo = (Dictionary<string, List<AnimationKeyInfo.AnmKeyInfo>>)akf_dictInfo.GetValue(__instance);

                List<AnimationKeyInfo.AnmKeyInfo> list = dictInfo[name];


                if (flag[0])
                {
                    value[0] = SliderMath.CalculatePosition(list, rate);
                }
                if (flag[1])
                {
                    value[1] = SliderMath.SafeCalculateRotation(value[1], name, list, rate);
                }
                if (flag[2])
                {
                    value[2] = SliderMath.CalculateScale(list, rate);
                }
            }
        }
    }
}
