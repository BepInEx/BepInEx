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

            UnityEngine.Debug.logger.LogWarning("inject", "Loaded!!!");

            loaded = true;
        }
    }
}
