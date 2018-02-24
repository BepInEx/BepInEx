using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtensibleSaveFormat
{
    public class ExtensibleSaveFormat : BaseUnityPlugin
    {
        public override string ID => "com.bepis.bepinex.extendedsave";

        public override string Name => "Extensible Save Format";

        public override Version Version => new Version("1.0");

        void Awake()
        {
            Hooks.InstallHooks();
        }

        internal static Dictionary<ChaFile, Dictionary<string, object>> internalDictionary = new Dictionary<ChaFile, Dictionary<string, object>>();

        #region Events

        public delegate void CardEventHandler(ChaFile file);

        public static event CardEventHandler CardBeingSaved;

        public static event CardEventHandler CardBeingLoaded;

        internal static void writeEvent(ChaFile file)
        {
            CardBeingSaved?.Invoke(file);
        }

        internal static void readEvent(ChaFile file)
        {
            CardBeingLoaded?.Invoke(file);
        }

        #endregion


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
