using System.Diagnostics;
using BepInEx.Unity.Bootstrap;

namespace BepInEx.Bootstrap
{
    public static class Linker
    {
        public static void StartBepInEx()
        {
            var chainloader = new UnityChainloader();

            chainloader.Initialize(Process.GetCurrentProcess().MainModule.FileName);
            chainloader.Execute();
        }
    }
}
