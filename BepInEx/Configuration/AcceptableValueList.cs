using System;
using System.Linq;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Specify the list of acceptable values for a setting.
	/// </summary>
	public class AcceptableValueList<T> : AcceptableValueBase where T : IEquatable<T>
	{
		/// <summary>
		/// List of values that a setting can take.
		/// </summary>
		public virtual T[] AcceptableValues { get; }

		/// <summary>
		/// Specify the list of acceptable values for a setting.
		/// If the setting does not equal any of the values, it will be set to the first one.
		/// </summary>
		public AcceptableValueList(params T[] acceptableValues) : base(typeof(T))
		{
			if (acceptableValues == null) throw new ArgumentNullException(nameof(acceptableValues));
			if (acceptableValues.Length == 0) throw new ArgumentException("At least one acceptable value is needed", nameof(acceptableValues));

			AcceptableValues = acceptableValues;
		}

		/// <inheritdoc />
		public override object Clamp(object value)
		{
			if (IsValid(value))
				return value;

			return AcceptableValues[0];
		}

		/// <inheritdoc />
		public override bool IsValid(object value)
		{
			return value is T v && AcceptableValues.Any(x => x.Equals(v));
		}

		/// <inheritdoc />
		public override string ToDescriptionString()
		{
			return "# Acceptable values: " + string.Join(", ", AcceptableValues.Select(x => x.ToString()).ToArray());
		}
	}
}
