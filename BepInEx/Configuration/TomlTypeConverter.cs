using System;
using System.Collections.Generic;
using System.Globalization;

namespace BepInEx.Configuration
{
	internal class TypeConverter
	{
		public Func<object, Type, string> ConvertToString { get; set; }
		public Func<string, Type, object> ConvertToObject { get; set; }
	}

	internal static class TomlTypeConverter
	{
		public static Dictionary<Type, TypeConverter> TypeConverters { get; } = new Dictionary<Type, TypeConverter>
		{
			[typeof(string)] = new TypeConverter
			{
				ConvertToString = (obj, type) => (string)obj,
				ConvertToObject = (str, type) => str,
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

			[typeof(Enum)] = new TypeConverter
			{
				ConvertToString = (obj, type) => obj.ToString(),
				ConvertToObject = (str, type) => Enum.Parse(type, str, true),
			},
		};

		public static string ConvertToString(object value, Type valueType)
		{
			var conv = GetConverter(valueType);
			if (conv == null)
				throw new InvalidOperationException($"Cannot convert from type {valueType}");

			return conv.ConvertToString(value, valueType);
		}

		public static T ConvertToValue<T>(string value)
		{
			return (T)ConvertToValue(value, typeof(T));
		}

		public static object ConvertToValue(string value, Type valueType)
		{
			var conv = GetConverter(valueType);
			if (conv == null)
				throw new InvalidOperationException($"Cannot convert to type {valueType}");

			return conv.ConvertToObject(value, valueType);
		}

		private static TypeConverter GetConverter(Type valueType)
		{
			if (valueType.IsEnum)
				return TypeConverters[typeof(Enum)];

			return TypeConverters[valueType];
		}

		public static bool CanConvert(Type type)
		{
			return GetConverter(type) != null;
		}

		public static IEnumerable<Type> GetSupportedTypes()
		{
			return TypeConverters.Keys;
		}
	}
}
