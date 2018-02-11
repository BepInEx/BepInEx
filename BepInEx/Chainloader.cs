using ChaCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public class Chainloader
    {
        static bool loaded = false;

        public static void Initialize()
        {
            if (loaded)
                return;

            UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
            Console.WriteLine("Chainloader started");
            
            BepInComponent.Create();

            loaded = true;
        }
    }
}
