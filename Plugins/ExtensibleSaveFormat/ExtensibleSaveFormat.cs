using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtensibleSaveFormat
{
    public class ExtensibleSaveFormat : BaseUnityPlugin
    {
        public override string ID => "extendedsave";

        public override string Name => "Extensible Save Format";

        public override Version Version => new Version("1.0");


        internal static Dictionary<ChaFile, Dictionary<string, object>> internalDictionary = new Dictionary<ChaFile, Dictionary<string, object>>();

        public ExtensibleSaveFormat()
        {
            Hooks.InstallHooks();
        }


        public static bool TryGetExtendedFormat(ChaFile file, out Dictionary<string, object> extendedFormatData)
        {
            return internalDictionary.TryGetValue(file, out extendedFormatData);
        }

        public static void SetExtendedFormat(ChaFile file, Dictionary<string, object> extendedFormatData)
        {
            internalDictionary[file] = extendedFormatData;
        }
    }
}
