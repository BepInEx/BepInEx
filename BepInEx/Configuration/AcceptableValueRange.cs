using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Specify the range of acceptable values for a setting.
	/// </summary>
	public class AcceptableValueRange<T> : AcceptableValueBase where T : IComparable
	{
		/// <param name="minValue">Lowest acceptable value</param>
		/// <param name="maxValue">Highest acceptable value</param>
		public AcceptableValueRange(T minValue, T maxValue) : base(typeof(T))
		{
			if (maxValue == null)
				throw new ArgumentNullException(nameof(maxValue));
			if (minValue == null)
				throw new ArgumentNullException(nameof(minValue));
			if (minValue.CompareTo(maxValue) >= 0)
				throw new ArgumentException($"{nameof(minValue)} has to be lower than {nameof(maxValue)}");

			MinValue = minValue;
			MaxValue = maxValue;
		}

		/// <summary>
		/// Lowest acceptable value
		/// </summary>
		public virtual T MinValue { get; }

		/// <summary>
		/// Highest acceptable value
		/// </summary>
		public virtual T MaxValue { get; }

		/// <inheritdoc />
		public override object Clamp(object value)
		{
			if (MinValue.CompareTo(value) > 0)
				return MinValue;

			if (MaxValue.CompareTo(value) < 0)
				return MaxValue;

			return value;
		}

		/// <inheritdoc />
		public override bool IsValid(object value)
		{
			return MinValue.CompareTo(value) <= 0 && MaxValue.CompareTo(value) >= 0;
		}

		/// <inheritdoc />
		public override string ToDescriptionString()
		{
			return $"# Acceptable value range: From {MinValue} to {MaxValue}";
		}
	}
}
