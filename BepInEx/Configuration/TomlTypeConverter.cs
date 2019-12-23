using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Serializer/deserializer used by the config system.
	/// </summary>
	public static class TomlTypeConverter
	{
		// Don't put anything from UnityEngine here or it will break preloader, use LazyTomlConverterLoader instead
		private static Dictionary<Type, TypeConverter> TypeConverters { get; } = new Dictionary<Type, TypeConverter>
		{
			[typeof(string)] = new TypeConverter
			{
				ConvertToString = (obj, type) => Escape((string)obj),
				ConvertToObject = (str, type) =>
				{
					// Check if the string is a file path with unescaped \ path separators (e.g. D:\test and not D:\\test)
					if (Regex.IsMatch(str, @"^""?\w:\\(?!\\)(?!.+\\\\)"))
						return str;
					return Unescape(str);
				},
			},
			[typeof(bool)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString().ToLowerInvariant(),
				ConvertToObject = (str, type) => bool.Parse(str),
			},
			[typeof(byte)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => byte.Parse(str),
			},

			//integral types

			[typeof(sbyte)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => sbyte.Parse(str),
			},
			[typeof(byte)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => byte.Parse(str),
			},
			[typeof(short)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => short.Parse(str),
			},
			[typeof(ushort)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => ushort.Parse(str),
			},
			[typeof(int)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => int.Parse(str),
			},
			[typeof(uint)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => uint.Parse(str),
			},
			[typeof(long)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => long.Parse(str),
			},
			[typeof(ulong)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => ulong.Parse(str),
			},

			//floating point types

			[typeof(float)] = new TypeConverter
			{
				ConvertToString = (obj, type) => ((float)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str, type) => float.Parse(str, NumberFormatInfo.InvariantInfo),
			},
			[typeof(double)] = new TypeConverter
			{
				ConvertToString = (obj, type) => ((double)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str, type) => double.Parse(str, NumberFormatInfo.InvariantInfo),
			},
			[typeof(decimal)] = new TypeConverter
			{
				ConvertToString = (obj, type) => ((decimal)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str, type) => decimal.Parse(str, NumberFormatInfo.InvariantInfo),
			},

			//enums are special

			[typeof(Enum)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => Enum.Parse(type, str, true),
			},
		};

		/// <summary>
		/// Convert object of a given type to a string using available converters.
		/// </summary>
		public static string ConvertToString(object value, Type valueType)
		{
			var conv = GetConverter(valueType);
			if (conv == null)
				throw new InvalidOperationException($"Cannot convert from type {valueType}");

			return conv.ConvertToString(value, valueType);
		}

		/// <summary>
		/// Convert string to an object of a given type using available converters.
		/// </summary>
		public static T ConvertToValue<T>(string value)
		{
			return (T)ConvertToValue(value, typeof(T));
		}

		/// <summary>
		/// Convert string to an object of a given type using available converters.
		/// </summary>
		public static object ConvertToValue(string value, Type valueType)
		{
			var conv = GetConverter(valueType);
			if (conv == null)
				throw new InvalidOperationException($"Cannot convert to type {valueType.Name}");

			return conv.ConvertToObject(value, valueType);
		}

		/// <summary>
		/// Get a converter for a given type if there is any.
		/// </summary>
		public static TypeConverter GetConverter(Type valueType)
		{
			if (valueType == null) throw new ArgumentNullException(nameof(valueType));

			if (valueType.IsEnum)
				return TypeConverters[typeof(Enum)];

			if (!TypeConverters.TryGetValue(valueType, out var result) && !_lazyLoadedConverters)
			{
				_lazyLoadedConverters = true;
				LazyLoadConverters();
				TypeConverters.TryGetValue(valueType, out result);
			}
			return result;
		}

		/// <summary>
		/// Add a new type converter for a given type. 
		/// If a different converter is already added, this call is ignored and false is returned.
		/// </summary>
		public static bool AddConverter(Type type, TypeConverter converter)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			if (converter == null) throw new ArgumentNullException(nameof(converter));
			if (CanConvert(type))
			{
				Logger.LogWarning("Tried to add a TomlConverter when one already exists for type " + type.FullName);
				return false;
			}

			TypeConverters.Add(type, converter);
			return true;
		}

		/// <summary>
		/// Check if a given type can be converted to and from string.
		/// </summary>
		public static bool CanConvert(Type type)
		{
			return GetConverter(type) != null;
		}

		/// <summary>		
		/// Give a list of types with registered converters.
		/// </summary>
		public static IEnumerable<Type> GetSupportedTypes()
		{
			return TypeConverters.Keys;
		}

		private static bool _lazyLoadedConverters;
		private static void LazyLoadConverters()
		{
			try { LazyTomlConverterLoader.AddUnityEngineConverters(); }
			catch (Exception ex) { Logger.LogWarning("Failed to load UnityEngine Toml converters - " + ex.Message); }
		}

		private static string Escape(this string txt)
		{
			if (string.IsNullOrEmpty(txt)) return string.Empty;

			var stringBuilder = new StringBuilder(txt.Length + 2);
			foreach (char c in txt)
				switch (c)
				{
					case '\0':
						stringBuilder.Append(@"\0");
						break;
					case '\a':
						stringBuilder.Append(@"\a");
						break;
					case '\b':
						stringBuilder.Append(@"\b");
						break;
					case '\t':
						stringBuilder.Append(@"\t");
						break;
					case '\n':
						stringBuilder.Append(@"\n");
						break;
					case '\v':
						stringBuilder.Append(@"\v");
						break;
					case '\f':
						stringBuilder.Append(@"\f");
						break;
					case '\r':
						stringBuilder.Append(@"\r");
						break;
					case '\'':
						stringBuilder.Append(@"\'");
						break;
					case '\\':
						stringBuilder.Append(@"\");
						break;
					case '\"':
						stringBuilder.Append(@"\""");
						break;
					default:
						stringBuilder.Append(c);
						break;
				}
			return stringBuilder.ToString();
		}

		private static string Unescape(this string txt)
		{
			if (string.IsNullOrEmpty(txt))
				return txt;
			var stringBuilder = new StringBuilder(txt.Length);
			for (int i = 0; i < txt.Length;)
			{
				int num = txt.IndexOf('\\', i);
				if (num < 0 || num == txt.Length - 1)
					num = txt.Length;
				stringBuilder.Append(txt, i, num - i);
				if (num >= txt.Length)
					break;
				char c = txt[num + 1];
				switch (c)
				{
					case '0':
						stringBuilder.Append('\0');
						break;
					case 'a':
						stringBuilder.Append('\a');
						break;
					case 'b':
						stringBuilder.Append('\b');
						break;
					case 't':
						stringBuilder.Append('\t');
						break;
					case 'n':
						stringBuilder.Append('\n');
						break;
					case 'v':
						stringBuilder.Append('\v');
						break;
					case 'f':
						stringBuilder.Append('\f');
						break;
					case 'r':
						stringBuilder.Append('\r');
						break;
					case '\'':
						stringBuilder.Append('\'');
						break;
					case '\"':
						stringBuilder.Append('\"');
						break;
					case '\\':
						stringBuilder.Append('\\');
						break;
					default:
						stringBuilder.Append('\\').Append(c);
						break;
				}

				i = num + 2;
			}

			return stringBuilder.ToString();
		}
	}

	/// <summary>
	/// For types that are in assemblies that can't get loaded before preloader runs (or it won't work on these assemblies)
	/// </summary>
	internal static class LazyTomlConverterLoader
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void AddUnityEngineConverters()
		{
			var colorConverter = new TypeConverter
			{
				ConvertToString = (obj, type) => ColorUtility.ToHtmlStringRGBA((Color)obj),
				ConvertToObject = (str, type) =>
				{
					if (!ColorUtility.TryParseHtmlString("#" + str.Trim('#', ' '), out var c))
						throw new FormatException("Invalid color string, expected hex #RRGGBBAA");
					return c;
				},
			};

			TomlTypeConverter.AddConverter(typeof(Color), colorConverter);

			var jsonConverter = new TypeConverter
			{
				ConvertToString = (obj, type) => JsonUtility.ToJson(obj),
				ConvertToObject = (str, type) => JsonUtility.FromJson(type: type, json: str),
			};

			TomlTypeConverter.AddConverter(typeof(Vector2), jsonConverter);
			TomlTypeConverter.AddConverter(typeof(Vector3), jsonConverter);
			TomlTypeConverter.AddConverter(typeof(Vector4), jsonConverter);
			TomlTypeConverter.AddConverter(typeof(Quaternion), jsonConverter);
		}
	}
}