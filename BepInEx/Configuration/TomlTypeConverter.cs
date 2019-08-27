using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Serializer/deserializer used by the config system.
	/// </summary>
	public static class TomlTypeConverter
	{
		// Don't put anything from UnityEngine here or it will break preloader (loads the assembly before it's patched)
		private static Dictionary<Type, TypeConverter> TypeConverters { get; } = new Dictionary<Type, TypeConverter>
		{
			[typeof(string)] = new TypeConverter
			{
				ConvertToString = (obj, type) => Regex.Escape((string)obj),
				ConvertToObject = (str, type) =>
				{
					// Check if the string is a file path with unescaped \ path separators (e.g. D:\test and not D:\\test)
					if (Regex.IsMatch(str, @"^""?\w:\\(?!\\)(?!.+\\\\)"))
						return str;
					return Regex.Unescape(str);
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

			TypeConverters.TryGetValue(valueType, out var result);
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
			if (CanConvert(type)) return false;

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
	}
}