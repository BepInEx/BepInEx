using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Configuration
{
	/// <summary>
	/// A keyboard shortcut that can be used in Update method to check if user presses a key combo. The shortcut is only
	/// triggered when the user presses the exact combination. For example, <c>F + LeftCtrl</c> will trigger only if user 
	/// presses and holds only LeftCtrl, and then presses F. If any other keys are pressed, the shortcut will not trigger.
	/// 
	/// Can be used as a value of a setting in <see cref="ConfigFile.Bind{T}(ConfigDefinition,T,ConfigDescription)"/> 
	/// to allow user to change this shortcut and have the changes saved.
	/// 
	/// How to use: Use <see cref="IsDown"/> in this class instead of <see cref="Input.GetKeyDown(KeyCode)"/> in the Update loop.
	/// </summary>
	public struct KeyboardShortcut
	{
		static KeyboardShortcut()
		{
			TomlTypeConverter.AddConverter(
				typeof(KeyboardShortcut),
				new TypeConverter
				{
					ConvertToString = (o, type) => ((KeyboardShortcut)o).Serialize(),
					ConvertToObject = (s, type) => Deserialize(s)
				});
		}

		/// <summary>
		/// Shortcut that never triggers.
		/// </summary>
		public static readonly KeyboardShortcut Empty = new KeyboardShortcut();

		/// <summary>
		/// All KeyCode values that can be used in a keyboard shortcut.
		/// </summary>
		[Obsolete("Use UnityInput.Current.SupportedKeyCodes instead")]
		public static readonly IEnumerable<KeyCode> AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

		// Don't block hotkeys if mouse is being pressed, e.g. when shooting and trying to strafe
		private static KeyCode[] _modifierBlockKeyCodes;

		private readonly KeyCode[] _allKeys;

		/// <summary>
		/// Create a new keyboard shortcut.
		/// </summary>
		/// <param name="mainKey">Main key to press</param>
		/// <param name="modifiers">Keys that should be held down before main key is registered</param>
		public KeyboardShortcut(KeyCode mainKey, params KeyCode[] modifiers) : this(new[] { mainKey }.Concat(modifiers).ToArray())
		{
			if (mainKey == KeyCode.None && modifiers.Any())
				throw new ArgumentException($"Can't set {nameof(mainKey)} to KeyCode.None if there are any {nameof(modifiers)}");
		}

		private KeyboardShortcut(KeyCode[] keys)
		{
			_allKeys = SanitizeKeys(keys);
		}

		private static KeyCode[] SanitizeKeys(params KeyCode[] keys)
		{
			if (keys.Length == 0 || keys[0] == KeyCode.None)
				return new[] { KeyCode.None };

			return new[] { keys[0] }.Concat(keys.Skip(1).Distinct().Where(x => x != keys[0]).OrderBy(x => (int)x)).ToArray();
		}

		/// <summary>
		/// Main key of the key combination. It has to be pressed / let go last for the combination to be triggered.
		/// If the combination is empty, <see cref="KeyCode.None"/> is returned.
		/// </summary>
		public KeyCode MainKey => _allKeys != null && _allKeys.Length > 0 ? _allKeys[0] : KeyCode.None;

		/// <summary>
		/// Modifiers of the key combination, if any.
		/// </summary>
		public IEnumerable<KeyCode> Modifiers => _allKeys?.Skip(1) ?? Enumerable.Empty<KeyCode>();

		/// <summary>
		/// Attempt to deserialize key combination from the string.
		/// </summary>
		public static KeyboardShortcut Deserialize(string str)
		{
			try
			{
				var parts = str.Split(new[] { ' ', '+', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
							   .Select(x => (KeyCode)Enum.Parse(typeof(KeyCode), x)).ToArray();
				return new KeyboardShortcut(parts);
			}
			catch (SystemException ex)
			{
				Logger.Log(LogLevel.Error, "Failed to read keybind from settings: " + ex.Message);
				return Empty;
			}
		}

		/// <summary>
		/// Serialize the key combination into a user readable string.
		/// </summary>
		public string Serialize()
		{
			if (_allKeys == null) return string.Empty;
			return string.Join(" + ", _allKeys.Select(x => x.ToString()).ToArray());
		}

		/// <summary>
		/// Check if the main key was just pressed (Input.GetKeyDown), and specified modifier keys are all pressed
		/// </summary>
		public bool IsDown()
		{
			var mainKey = MainKey;
			if (mainKey == KeyCode.None) return false;

			return UnityInput.Current.GetKeyDown(mainKey) && ModifierKeyTest();
		}

		/// <summary>
		/// Check if the main key is currently held down (Input.GetKey), and specified modifier keys are all pressed
		/// </summary>
		public bool IsPressed()
		{
			var mainKey = MainKey;
			if (mainKey == KeyCode.None) return false;

			return UnityInput.Current.GetKey(mainKey) && ModifierKeyTest();
		}

		/// <summary>
		/// Check if the main key was just lifted (Input.GetKeyUp), and specified modifier keys are all pressed.
		/// </summary>
		public bool IsUp()
		{
			var mainKey = MainKey;
			if (mainKey == KeyCode.None) return false;

			return UnityInput.Current.GetKeyUp(mainKey) && ModifierKeyTest();
		}

		private bool ModifierKeyTest()
		{
			var allKeys = _allKeys;
			var mainKey = MainKey;

			bool allModifiersPressed = allKeys.All(c => c == mainKey || UnityInput.Current.GetKey(c));
			if (!allModifiersPressed) return false;

			// Lazy init to make sure the game is initialized and we're in main thread before calling UnityInput.Current
			if (_modifierBlockKeyCodes == null)
				_modifierBlockKeyCodes = UnityInput.Current.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6, KeyCode.None }).ToArray();
			
			bool noOtherModifiersPressed = _modifierBlockKeyCodes.All(c => !UnityInput.Current.GetKey(c) || allKeys.Contains(c));
			return noOtherModifiersPressed;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			if (MainKey == KeyCode.None) return "Not set";

			return string.Join(" + ", _allKeys.Select(c => c.ToString()).ToArray());
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is KeyboardShortcut shortcut && MainKey == shortcut.MainKey && Modifiers.SequenceEqual(shortcut.Modifiers);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			if (MainKey == KeyCode.None) return 0;

			return _allKeys.Aggregate(_allKeys.Length, (current, item) => unchecked(current * 31 + (int)item));
		}
	}
}