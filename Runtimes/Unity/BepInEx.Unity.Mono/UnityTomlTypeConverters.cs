using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

namespace BepInEx.Unity.Mono;

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

        TomlTypeConverter.AddConverter(typeof(Color), colorConverter);

        var jsonConverter = new TypeConverter
        {
            ConvertToString = (obj, type) => JsonUtility.ToJson(obj),
            ConvertToObject = (str, type) => JsonUtility.FromJson(type: type, json: str)
        };

        TomlTypeConverter.AddConverter(typeof(Vector2), jsonConverter);
        TomlTypeConverter.AddConverter(typeof(Vector3), jsonConverter);
        TomlTypeConverter.AddConverter(typeof(Vector4), jsonConverter);
        TomlTypeConverter.AddConverter(typeof(Quaternion), jsonConverter);
        TomlTypeConverter.AddConverter(typeof(Rect), new TypeConverter
        {
            ConvertToObject = StringToRect,
            ConvertToString = RectToString
        });
    }

    private static object StringToRect(string s, Type type)
    {
        // JsonUtility doesn't work with Rect on all Unity versions, so parse it manually
        Rect result = default(Rect);

        if (s == null)
            return result;

        string cleaned = s.Trim('{', '}').Replace(" ", "");
        foreach (string part in cleaned.Split(','))
        {
            string[] parts = part.Split(':');
            if (parts.Length != 2 || !float.TryParse(parts[1], out float value))
                continue;

            string id = parts[0].Trim('"');
            if (id == "x")
                result.x = value;
            else if (id == "y")
                result.y = value;
            else if (id == "width" || id == "z")
                result.width = value;
            else if (id == "height" || id == "w")
                result.height = value;
        }

        return result;
    }

    private static string RectToString(object o, Type type)
    {
        Rect rect = (Rect)o;
        return string.Format(CultureInfo.InvariantCulture,
                             "{{ \"x\":{0}, \"y\":{1}, \"width\":{2}, \"height\":{3} }}",
                             rect.x, rect.y, rect.width, rect.height);
    }
}
