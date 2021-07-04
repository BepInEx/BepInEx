using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

namespace BepInEx.Unity
{
    /// <summary>
    ///     Config types that are unity specific
    /// </summary>
    internal static class UnityTomlTypeConverters
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddUnityEngineConverters()
        {
            var colorConverter = new TypeConverter
            {
                ConvertToString = (obj, type) => ColorUtility.ToHtmlStringRGBA((Color) obj),
                ConvertToObject = (str, type) =>
                {
                    if (!ColorUtility.TryParseHtmlString("#" + str.Trim('#', ' '), out var c))
                        throw new FormatException("Invalid color string, expected hex #RRGGBBAA");
                    return c;
                }
            };

            ConfigTypeConverter.AddConverter(typeof(Color), colorConverter);

            var jsonConverter = new TypeConverter
            {
                ConvertToString = (obj, type) => JsonUtility.ToJson(obj),
                ConvertToObject = (str, type) => JsonUtility.FromJson(type: type, json: str)
            };

            ConfigTypeConverter.AddConverter(typeof(Vector2), jsonConverter);
            ConfigTypeConverter.AddConverter(typeof(Vector3), jsonConverter);
            ConfigTypeConverter.AddConverter(typeof(Vector4), jsonConverter);
            ConfigTypeConverter.AddConverter(typeof(Quaternion), jsonConverter);
        }
    }
}
