using BepInEx;
using ChaCustom;
using Harmony;
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

        public SliderUnlocker()
        {


            var harmony = HarmonyInstance.Create("com.bepis.bepinex.sliderunlocker");

            MethodInfo original = AccessTools.Method(typeof(CustomBase), "ConvertTextFromRate");

            HarmonyMethod postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("ConvertTextFromRateHook"));
            
            harmony.Patch(original, null, postfix);



            original = AccessTools.Method(typeof(CustomBase), "ConvertRateFromText");

            postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("ConvertRateFromTextHook"));

            harmony.Patch(original, null, postfix);

            Console.WriteLine("test");

            //foreach (var method in typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name.Contains("GetInfo")))
            //{typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name == "GetInfo");
            //    Console.WriteLine(method.GetParameters().Select(x => x.ParameterType.FullName).Aggregate((a, b) => $"{a};{b}"));
            //}


            original = typeof(AnimationKeyInfo).GetMethods().Where(x => x.Name.Contains("GetInfo")).ToArray()[1];

            //original = AccessTools.Method(typeof(AnimationKeyInfo), "GetInfo"); //new Type[] { typeof(string), typeof(float), typeof(Vector3[]), typeof(bool[]) }

            postfix = new HarmonyMethod(typeof(SliderUnlocker).GetMethod("GetInfoHook"));

            harmony.Patch(original, null, postfix);

            Console.WriteLine("hooked");
        }

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (Slider gameObject in GameObject.FindObjectsOfType<Slider>())
            {
                gameObject.maxValue = 2;
            }
        }

        //TypeDefinition customBase = assembly.MainModule.Types.First(x => x.Name == "CustomBase");

        //var methods = customBase.Methods;

        //var convertTextFromRate = methods.First(x => x.Name == "ConvertTextFromRate");

        //var IL = convertTextFromRate.Body.GetILProcessor();
        //IL.Replace(convertTextFromRate.Body.Instructions[0], IL.Create(OpCodes.Ldc_I4, -0));
        //    IL.Replace(convertTextFromRate.Body.Instructions[2], IL.Create(OpCodes.Ldc_I4, 200));
            
        //    var convertRateFromText = methods.First(x => x.Name == "ConvertRateFromText");

        //IL = convertRateFromText.Body.GetILProcessor();
        //    IL.Replace(convertRateFromText.Body.Instructions[11], IL.Create(OpCodes.Ldc_I4, -0));
        //    IL.Replace(convertRateFromText.Body.Instructions[13], IL.Create(OpCodes.Ldc_I4, 200));


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
        public static void GetInfoHook(AnimationKeyInfo __instance, bool __result, string name, float rate, ref Vector3[] value, bool[] flag)
        {
            if (rate > 1)
                Console.WriteLine(rate);

            rate *= 2f;

            if (!__result)
                return;

            var dictInfo = (Dictionary < string, List<AnimationKeyInfo.AnmKeyInfo> > )
                (typeof(AnimationKeyInfo).GetField("dictInfo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance));

            List<AnimationKeyInfo.AnmKeyInfo> list = dictInfo[name];

            //if (flag[0])
            //{
            //    Vector3 min = list[0].pos;
            //    Vector3 max = list[list.Count - 1].pos;

            //    Vector3 diff = new Vector3(max.x - min.x,
            //                                max.y - min.y,
            //                                max.z - min.z);

            //    value[0] = new Vector3(min.x + (diff.x * rate),
            //                            min.y + (diff.y * rate),
            //                            min.z + (diff.z * rate));
            //}
            //if (flag[1])
            //{
            //    //if (rate == 0f)
            //    //{
            //    //    value[1] = list[0].rot;
            //    //}
            //    //else if (rate == 1f)
            //    //{
            //    //    value[1] = list[list.Count - 1].rot;
            //    //}
            //    //else
            //    //{
            //    //    float num3 = (float)(list.Count - 1) * rate;
            //    //    int num4 = Mathf.FloorToInt(num3);
            //    //    float t2 = num3 - (float)num4;
            //    //    value[1].x = Mathf.LerpAngle(list[num4].rot.x, list[num4 + 1].rot.x, t2);
            //    //    value[1].y = Mathf.LerpAngle(list[num4].rot.y, list[num4 + 1].rot.y, t2);
            //    //    value[1].z = Mathf.LerpAngle(list[num4].rot.z, list[num4 + 1].rot.z, t2);
            //    //}

            //    Vector3 min = list[0].rot;
            //    Vector3 max = list[list.Count - 1].rot;

            //    Vector3 diff = new Vector3(max.x - min.x,
            //                                max.y - min.y,
            //                                max.z - min.z);

            //    value[1] = new Vector3(min.x + (diff.x * rate),
            //                            min.y + (diff.y * rate),
            //                            min.z + (diff.z * rate));
            //}
            if (flag[2])
            {
                Vector3 min = list[0].scl;
                Vector3 max = list[list.Count - 1].scl;

                Vector3 diff = new Vector3(max.x - min.x,
                                            max.y - min.y,
                                            max.z - min.z);

                value[2] = new Vector3(min.x + (diff.x * rate),
                                        min.y + (diff.y * rate),
                                        min.z + (diff.z * rate));
            }
        }
    }
}