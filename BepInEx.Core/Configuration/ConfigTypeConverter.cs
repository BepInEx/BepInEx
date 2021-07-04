using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInEx.Configuration
{
    /// <summary>
    ///     Serializer/deserializer used by the config system.
    /// </summary>
    public static class ConfigTypeConverter
    {
        // Don't put anything from UnityEngine here or it will break preloader, use LazyTomlConverterLoader instead
        private static Dictionary<Type, TypeConverter> DirectConverters { get; } = new()
        {
            [typeof(string)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(), //Escape((string) obj),
                ConvertToObject = (str, type) => str,
                // {
                //     // Check if the string is a file path with unescaped \ path separators (e.g. D:\test and not D:\\test)
                //     if (Regex.IsMatch(str, @"^""?\w:\\(?!\\)(?!.+\\\\)"))
                //         return str;
                //     return Unescape(str);
                // }
            },
            [typeof(bool)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString().ToLowerInvariant(),
                ConvertToObject = (str, type) => bool.Parse(str)
            },
            [typeof(byte)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => byte.Parse(str)
            },

            //integral types

            [typeof(sbyte)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => sbyte.Parse(str)
            },
            [typeof(byte)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => byte.Parse(str)
            },
            [typeof(short)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => short.Parse(str)
            },
            [typeof(ushort)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => ushort.Parse(str)
            },
            [typeof(int)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => int.Parse(str)
            },
            [typeof(uint)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => uint.Parse(str)
            },
            [typeof(long)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => long.Parse(str)
            },
            [typeof(ulong)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => ulong.Parse(str)
            },

            //floating point types

            [typeof(float)] = new TypeConverter
            {
                ConvertToString = (obj, type) => ((float) obj).ToString(NumberFormatInfo.InvariantInfo),
                ConvertToObject = (str, type) => float.Parse(str, NumberFormatInfo.InvariantInfo)
            },
            [typeof(double)] = new TypeConverter
            {
                ConvertToString = (obj, type) => ((double) obj).ToString(NumberFormatInfo.InvariantInfo),
                ConvertToObject = (str, type) => double.Parse(str, NumberFormatInfo.InvariantInfo)
            },
            [typeof(decimal)] = new TypeConverter
            {
                ConvertToString = (obj, type) => ((decimal) obj).ToString(NumberFormatInfo.InvariantInfo),
                ConvertToObject = (str, type) => decimal.Parse(str, NumberFormatInfo.InvariantInfo)
            },

            // Special direct converters

            [typeof(Enum)] = new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => Enum.Parse(type, str, true)
            },
            [typeof(DateTime)] = new TypeConverter
            {
                ConvertToString = (obj, type) => ((DateTime) obj).ToString("O"),
                ConvertToObject = (str, type) => DateTime.ParseExact(str, "O", CultureInfo.InvariantCulture)
            },
            [typeof(DateTimeOffset)] = new TypeConverter
            {
                ConvertToString = (obj, type) => ((DateTimeOffset) obj).ToString("O"),
                ConvertToObject = (str, type) => DateTimeOffset.ParseExact(str, "O", CultureInfo.InvariantCulture)
            }
        };
        
        /// <summary>
        ///     Convert object of a given type to a string using available converters.
        /// </summary>
        public static string ConvertToString(object value, Type valueType)
        {
            var conv = GetConverter(valueType);
            if (conv == null)
                throw new InvalidOperationException($"Cannot convert from type {valueType}");

            return conv.ConvertToString(value, valueType);
        }

        /// <summary>
        ///     Convert string to an object of a given type using available converters.
        /// </summary>
        public static T ConvertToValue<T>(string value) => (T) ConvertToValue(value, typeof(T));

        /// <summary>
        ///     Convert string to an object of a given type using available converters.
        /// </summary>
        public static object ConvertToValue(string value, Type valueType)
        {
            var conv = GetConverter(valueType);
            if (conv == null)
                throw new InvalidOperationException($"Cannot convert to type {valueType.Name}");

            return conv.ConvertToObject(value, valueType);
        }

        /// <summary>
        ///     Get a converter for a given type if there is any.
        /// </summary>
        public static TypeConverter GetConverter(Type valueType)
        {
            if (valueType == null)
                throw new ArgumentNullException(nameof(valueType));

            if (valueType.IsEnum)
                return DirectConverters[typeof(Enum)];

            DirectConverters.TryGetValue(valueType, out var result);

            return result;
        }

        /// <summary>
        ///     Add a new type converter for a given type.
        ///     If a different converter is already added, this call is ignored and false is returned.
        /// </summary>
        public static bool AddConverter(Type type, TypeConverter converter)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            if (CanConvertDirectly(type))
            {
                Logger.LogWarning("Tried to add a TomlConverter when one already exists for type " + type.FullName);
                return false;
            }

            DirectConverters.Add(type, converter);
            return true;
        }

        /// <summary>
        ///     Check if a given type can be converted to and from string.
        /// </summary>
        public static bool CanConvertDirectly(Type type) => GetConverter(type) != null;

        /// <summary>
        ///     Give a list of types with registered converters.
        /// </summary>
        public static IEnumerable<Type> GetSupportedTypes() => DirectConverters.Keys;

        public static object GetValue(this IConfigurationProvider provider, string[] path, Type type)
        {
            // We have direct value; simply resolve it normally
            if (CanConvertDirectly(type))
            {
                var val = provider.Get(path);
                return val != null ? ConvertToValue(val.Value, type) : null;
            }

            if (type.IsArray)
            {
                var elType = type.GetElementType();
                var indexPath = path.AddItem("0").ToArray();
                var values = new List<object>();
                for (var i = 0;; i++)
                {
                    indexPath[^1] = i.ToString(CultureInfo.InvariantCulture);
                    var val = provider.GetValue(indexPath, elType);
                    if (val == null)
                        break;
                    values.Add(val);
                }
                var arr = Array.CreateInstance(elType, values.Count);
                for (var i = 0; i < values.Count; i++)
                    arr.SetValue(values[i], i);
                return arr;
            } 
            
            if (typeof(IList<>).IsAssignableFrom(type))
            {
                var res = Activator.CreateInstance(type);
                var elType = type.GetGenericArguments()[0];
                var add = MethodInvoker.GetHandler(AccessTools.Method(type, "Add", new[] { elType }));
                var indexPath = path.AddItem("0").ToArray();
                for (var i = 0;; i++)
                {
                    indexPath[^1] = i.ToString(CultureInfo.InvariantCulture);
                    var val = provider.GetValue(indexPath, elType);
                    if (val == null)
                        break;
                    add(val);
                }

                return res;
            }
            
            if (typeof(IDictionary<,>).IsAssignableFrom(type))
            {
                var res = Activator.CreateInstance(type);
                var gArgs = type.GetGenericArguments();
                if (gArgs[0] != typeof(string))
                    throw new Exception("Only dictionaries with string keys are supported");
                var elType = gArgs[1];
                var add = MethodInvoker.GetHandler(AccessTools.Method(type, "Add", new[] { typeof(string), elType }));
                
                foreach (var subItem in provider.EntryPaths.Where(p => !p.SequenceEqual(path) && p.StartsWith(path)))
                {
                    var val = provider.GetValue(subItem, elType);
                    var key = string.Join(ConfigFile.PathSeparator.ToString(), subItem.Skip(path.Length).ToArray());
                    add(key, val);
                }

                return res;
            }

            var result = Activator.CreateInstance(type);
                        
            foreach (var fieldInfo in type.GetFields(AccessTools.all).Where(f => !f.IsInitOnly))
            {
                var fVal = provider.GetValue(path.AddToArray(fieldInfo.Name), fieldInfo.FieldType);
                if (fVal != null)
                    fieldInfo.SetValue(fieldInfo.IsStatic ? null : result, fVal);
            }
            
            foreach (var propertyInfo in type.GetProperties(AccessTools.all).Where(p => p.CanWrite))
            {
                var pVal = provider.GetValue(path.AddToArray(propertyInfo.Name), propertyInfo.PropertyType);
                // TODO: Support indexed props?
                if (pVal != null)
                    propertyInfo.SetValue(propertyInfo.GetSetMethod(true).IsStatic ? null : result, pVal, null);
            }

            return result;
        }

        private static bool StartsWith(this IList<string> arr, ICollection<string> prefix)
        {
            if (arr.Count < prefix.Count) return false;
            return !prefix.Where((t, i) => t != arr[i]).Any();
        }
    }
}
