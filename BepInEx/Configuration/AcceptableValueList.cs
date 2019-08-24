using System;
using System.Linq;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Specify the list of acceptable values for a setting.
	/// </summary>
	public sealed class AcceptableValueList<T> : AcceptableValueBase where T : IEquatable<T>
	{
		private readonly T[] _acceptableValues;

		/// <summary>
		/// Specify the list of acceptable values for a setting.
		/// If the setting does not equal any of the values, it will be set to the first one.
		/// </summary>
		public AcceptableValueList(params T[] acceptableValues)
		{
			if (acceptableValues == null) throw new ArgumentNullException(nameof(acceptableValues));
			if (acceptableValues.Length == 0) throw new ArgumentException("At least one acceptable value is needed", nameof(acceptableValues));

			_acceptableValues = acceptableValues;
		}

		/// <inheritdoc />
		public override object Clamp(object value)
		{
			if (IsValid(value))
				return value;

			return _acceptableValues[0];
		}

		/// <inheritdoc />
		public override bool IsValid(object value)
		{
			return value is T v && _acceptableValues.Any(x => x.Equals(v));
		}

		/// <inheritdoc />
		public override string ToSerializedString()
		{
			return "# Acceptable values: " + string.Join(", ", _acceptableValues.Select(x => x.ToString()).ToArray());
		}
	}
}