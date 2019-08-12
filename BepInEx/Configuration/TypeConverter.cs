using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// A serializer/deserializer combo for some type(s). Used by the config system.
	/// </summary>
	public class TypeConverter
	{
		/// <summary>
		/// Used to serialize the type into a (hopefully) human-readable string.
		/// Object is the instance to serialize, Type is the object's type.
		/// </summary>
		public Func<object, Type, string> ConvertToString { get; set; }

		/// <summary>
		/// Used to deserialize the type from a string.
		/// String is the data to deserialize, Type is the object's type, should return instance to an object of Type.
		/// </summary>
		public Func<string, Type, object> ConvertToObject { get; set; }
	}
}