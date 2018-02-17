using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SliderUnlocker
{
    public static class Hooks
    {
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
