using System;
using System.Collections.ObjectModel;

namespace BepInEx.Configuration
{
	internal static class TomlTypeConverter
	{
		public static ReadOnlyCollection<Type> SupportedTypes { get; } = new ReadOnlyCollection<Type>(new[]
		{
			typeof(string),
			typeof(int),
			typeof(bool)
		});

		public static string ConvertToString(object value)
		{
			Type valueType = value.GetType();

			if (!SupportedTypes.Contains(valueType))
				throw new InvalidOperationException($"Cannot convert from type {valueType}");

			if (value is string s)
			{
				return s;
			}

			if (value is int i)
			{
				return i.ToString();
			}

			if (value is bool b)
			{
				return b.ToString().ToLowerInvariant();
			}

			throw new NotImplementedException("Supported type does not have a converter");
		}

		public static T ConvertToValue<T>(string value)
		{
			if (!SupportedTypes.Contains(typeof(T)))
				throw new InvalidOperationException($"Cannot convert to type {typeof(T)}");

			if (typeof(T) == typeof(string))
			{
				return (T)(object)value;
			}

			if (typeof(T) == typeof(int))
			{
				return (T)(object)int.Parse(value);
			}

			if (typeof(T) == typeof(bool))
			{
				return (T)(object)bool.Parse(value);
			}

			throw new NotImplementedException("Supported type does not have a converter");
		}
	}
}