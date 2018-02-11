using ChaCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public class Target
    {
        static bool loaded = false;

        public static void Initialize()
        {
            if (loaded)
                return;

            UnityEngine.Debug.logger.Log("inject: Loaded!!!");



            loaded = true;
        }

        public static void InitializeCustomBase() //CustomBase customBase
        {
            UnityEngine.Debug.logger.Log("inject: CustomBase loaded!!!");

            CustomBase customBase = Singleton<CustomBase>.Instance;

            foreach (var entry in customBase.lstSelectList)
            {
                UnityEngine.Debug.Log(entry.list[2]);
                entry.list[2] = "TL TEST TL TEST";
            }
        }
    }
}
