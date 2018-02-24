using Harmony;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ExtensibleSaveFormat
{
    public static class Hooks
    {
        public static void InstallHooks()
        {
            var harmony = HarmonyInstance.Create("com.bepis.bepinex.extensiblesaveformat");


            MethodInfo original = AccessTools.Method(typeof(ChaFile), "SaveFile", new[] { typeof(BinaryWriter), typeof(bool) });

            HarmonyMethod postfix = new HarmonyMethod(typeof(Hooks).GetMethod("SaveFileHook"));

            harmony.Patch(original, null, postfix);


            original = AccessTools.Method(typeof(ChaFile), "LoadFile", new[] { typeof(BinaryReader), typeof(bool), typeof(bool) });

            postfix = new HarmonyMethod(typeof(Hooks).GetMethod("LoadFileHook"));

            harmony.Patch(original, null, postfix);
        }

        public static void SaveFileHook(ChaFile __instance, bool __result, BinaryWriter bw, bool savePng)
        {
            if (!__result)
                return;

            ExtensibleSaveFormat.writeEvent(__instance);

            if (!ExtensibleSaveFormat.TryGetExtendedFormat(__instance, out Dictionary<string, object> extendedData))
                return;

            byte[] bytes = MessagePackSerializer.Serialize(extendedData);

            bw.Write((int)bytes.Length);
            bw.Write(bytes);
        }

        public static void LoadFileHook(ChaFile __instance, bool __result, BinaryReader br, bool noLoadPNG, bool noLoadStatus)
        {
            if (!__result)
                return;

            try
            {
                int length = br.ReadInt32();

                if (length > 0)
                {
                    byte[] bytes = br.ReadBytes(length);

                    ExtensibleSaveFormat.internalDictionary[__instance] = MessagePackSerializer.Deserialize<Dictionary<string, object>>(bytes);

                    return;
                }
            }
            catch (EndOfStreamException) { }

            //initialize a new dictionary since it doesn't exist
            ExtensibleSaveFormat.internalDictionary[__instance] = new Dictionary<string, object>();

            ExtensibleSaveFormat.readEvent(__instance);
        }
    }
}
