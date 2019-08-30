using System;

namespace BepInEx.Configuration
{
	/// <summary>
	/// Provides access to a single setting inside of a <see cref="Configuration.ConfigFile"/>.
	/// </summary>
	/// <typeparam name="T">Type of the setting.</typeparam>
	public sealed class ConfigWrapper<T> : ConfigEntryBase
	{
		/// <summary>
		/// Fired when the setting is changed. Does not detect changes made outside from this object.
		/// </summary>
		public event EventHandler SettingChanged;

		private T _typedValue;

		/// <summary>
		/// Value of this setting.
		/// </summary>
		public T Value
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
		public override object BoxedValue
		{
			get => Value;
			set => Value = (T)value;
		}

		internal ConfigWrapper(ConfigFile configFile, ConfigDefinition definition, T defaultValue) : base(configFile, definition, typeof(T), defaultValue)
		{
			configFile.SettingChanged += (sender, args) =>
			{
				if (args.ChangedSetting == this) SettingChanged?.Invoke(sender, args);
			};
		}
	}
}
