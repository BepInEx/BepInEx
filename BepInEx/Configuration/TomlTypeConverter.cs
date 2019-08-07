using System;
using System.Collections.Generic;
using System.Globalization;

namespace BepInEx.Configuration
{
	public class TypeConverter
	{
		public Func<object, string> ConvertToString { get; set; }
		public Func<string, object> ConvertToObject { get; set; }
	}

	internal static class TomlTypeConverter
	{
		public static Dictionary<Type, TypeConverter> TypeConverters { get; } = new Dictionary<Type, TypeConverter>
		{
			[typeof(string)] = new TypeConverter
			{
				ConvertToString = (obj) => (string)obj,
				ConvertToObject = (str) => str,
			},
			[typeof(bool)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString().ToLowerInvariant(),
				ConvertToObject = (str) => bool.Parse(str),
			},
			[typeof(byte)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => byte.Parse(str),
			},

			//integral types

			[typeof(sbyte)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => sbyte.Parse(str),
			},
			[typeof(byte)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => byte.Parse(str),
			},
			[typeof(short)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => short.Parse(str),
			},
			[typeof(ushort)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => ushort.Parse(str),
			},
			[typeof(int)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => int.Parse(str),
			},
			[typeof(uint)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => uint.Parse(str),
			},
			[typeof(long)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => long.Parse(str),
			},
			[typeof(ulong)] = new TypeConverter
			{
				ConvertToString = (obj) => obj.ToString(),
				ConvertToObject = (str) => ulong.Parse(str),
			},

			//floating point types

			[typeof(float)] = new TypeConverter
			{
				ConvertToString = (obj) => ((float)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str) => float.Parse(str, NumberFormatInfo.InvariantInfo),
			},
			[typeof(double)] = new TypeConverter
			{
				ConvertToString = (obj) => ((double)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str) => double.Parse(str, NumberFormatInfo.InvariantInfo),
			},
			[typeof(decimal)] = new TypeConverter
			{
				ConvertToString = (obj) => ((decimal)obj).ToString(NumberFormatInfo.InvariantInfo),
				ConvertToObject = (str) => decimal.Parse(str, NumberFormatInfo.InvariantInfo),
			},
		};

		public static string ConvertToString(object value)
		{
			Type valueType = value.GetType();

			if (!TypeConverters.ContainsKey(valueType))
				throw new InvalidOperationException($"Cannot convert from type {valueType}");

			return TypeConverters[valueType].ConvertToString(value);
		}

		public static T ConvertToValue<T>(string value)
		{
			if (!TypeConverters.ContainsKey(typeof(T)))
				throw new InvalidOperationException($"Cannot convert to type {typeof(T)}");

			return (T)TypeConverters[typeof(T)].ConvertToObject(value);
		}
	}
}
