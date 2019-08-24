namespace BepInEx.Configuration
{
	/// <inheritdoc />
	public class ConfigEntry<T> : ConfigEntryBase
	{
		private T _typedValue;
		internal ConfigEntry(ConfigFile configFile, ConfigDefinition definition, T defaultValue) : base(configFile, definition, typeof(T), defaultValue) { }

		/// <summary>
		/// Get or set the value of the setting without boxing.
		/// </summary>
		public T TypedValue
		{
			get => _typedValue;
			set
			{
				value = ClampValue(value);
				if (Equals(_typedValue, value))
					return;

				_typedValue = value;
				OnSettingChanged(this);
			}
		}

		/// <inheritdoc />
		public override object Value
		{
			get => TypedValue;
			set => TypedValue = (T)value;
		}
	}
}
