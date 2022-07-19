using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.Utils;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx
{
	/// <summary>
	///     Abstraction layer over Unity's input systems for use in universal plugins that need to use hotkeys.
	///     It can use either Input or Unity.InputSystem, depending on what's available. Input is preferred.
	///     WARNING: Use only inside of Unity's main thread!
	/// </summary>
	public static class UnityInput
	{
		private static IInputSystem _current;

		/// <summary>
		///     Best currently supported input system.
		/// </summary>
		public static IInputSystem Current
		{
			get
			{
				if (_current != null)
					return _current;

				try
				{
					try
					{
						Input.GetKeyDown(KeyCode.A);
						_current = new LegacyInputSystem();
						Logger.LogDebug("[UnityInput] Using LegacyInputSystem");
					}
					catch (InvalidOperationException)
					{
						_current = new NewInputSystem();
						Logger.LogDebug("[UnityInput] Using NewInputSystem");
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning($"[UnityInput] Failed to detect available input systems - {ex}");
					_current = new NullInputSystem();
				}

				return _current;
			}
		}

		/// <summary>
		///     True if the Input class is not disabled.
		/// </summary>
		public static bool LegacyInputSystemAvailable => Current is LegacyInputSystem;
	}

	/// <summary>
	///     Generic input system interface. Just barely good enough for hotkeys.
	/// </summary>
	public interface IInputSystem
	{
		/// <inheritdoc cref="Input.mousePosition" />
		Vector3 mousePosition { get; }

		/// <inheritdoc cref="Input.mouseScrollDelta" />
		Vector2 mouseScrollDelta { get; }

		/// <inheritdoc cref="Input.mousePresent" />
		bool mousePresent { get; }

		/// <inheritdoc cref="Input.anyKey" />
		bool anyKey { get; }

		/// <inheritdoc cref="Input.anyKeyDown" />
		bool anyKeyDown { get; }

		/// <summary>
		///     All KeyCodes supported by the current input system.
		/// </summary>
		IEnumerable<KeyCode> SupportedKeyCodes { get; }
		// No easy way to use these with the new input system
		//bool GetButton(string buttonName);
		//bool GetButtonDown(string buttonName);
		//bool GetButtonUp(string buttonName);

		/// <inheritdoc cref="Input.GetKey(string)" />
		bool GetKey(string name);

		/// <inheritdoc cref="Input.GetKey(KeyCode)" />
		bool GetKey(KeyCode key);

		/// <inheritdoc cref="Input.GetKeyDown(string)" />
		bool GetKeyDown(string name);

		/// <inheritdoc cref="Input.GetKeyDown(KeyCode)" />
		bool GetKeyDown(KeyCode key);

		/// <inheritdoc cref="Input.GetKeyUp(string)" />
		bool GetKeyUp(string name);

		/// <inheritdoc cref="Input.GetKeyUp(KeyCode)" />
		bool GetKeyUp(KeyCode key);


		/// <inheritdoc cref="Input.GetMouseButton(int)" />
		bool GetMouseButton(int button);

		/// <inheritdoc cref="Input.GetMouseButtonDown(int)" />
		bool GetMouseButtonDown(int button);

		/// <inheritdoc cref="Input.GetMouseButtonUp(int)" />
		bool GetMouseButtonUp(int button);

		/// <inheritdoc cref="Input.ResetInputAxes()" />
		void ResetInputAxes();
	}

	internal class NewInputSystem : IInputSystem
	{
		static bool initialized;
		static readonly Dictionary<KeyCode, int> keyCodeToIndex = new Dictionary<KeyCode, int>();

		private static readonly Dictionary<string, string> keyToKeyCodeNameRemap = new Dictionary<string, string>()
		{
			["LeftCtrl"] = "LeftControl",
			["RightCtrl"] = "RightControl",
			["LeftMeta"] = "LeftApple",
			["RightMeta"] = "RightApple",
			["ContextMenu"] = "Menu",
			["PrintScreen"] = "Print",
			["Enter"] = "Return",
		};

		private delegate bool GetButtonDelegate(KeyCode key);

		private delegate Vector2 GetMouseVectorDelegate();

		private delegate bool GetMouseStateDelegate();

		public NewInputSystem()
		{
			// Don't use static ctor, init only when the input is actually instantiated the first time
			Init();
		}

		static void Init()
		{
			if (initialized)
				return;
			// Unity.InputSystem should be within the assembly resolve chain
			var inputSystemAssembly = Assembly.Load("Unity.InputSystem");
			var keyEnum = inputSystemAssembly.GetType("UnityEngine.InputSystem.Key");
			foreach (var key in Enum.GetValues(keyEnum))
			{
				var name = key.ToString();
				var value = (int)key;
				if (name.StartsWith("Numpad"))
					name = name.Replace("Numpad", "Keypad");
				else if (name.StartsWith("Digit"))
					name = name.Replace("Digit", "Alpha");
				else if (keyToKeyCodeNameRemap.TryGetValue(name, out var remappedName))
					name = remappedName;

				if (TryEnumParse<KeyCode>(name, out var keyCode))
					keyCodeToIndex[keyCode] = value;
				else
					Logger.LogDebug($"[UnityInput] Unknown key name: {name}, skipping remapping");
			}

			var keyCodeToIndexField = AccessTools.Field(typeof(NewInputSystem), nameof(keyCodeToIndex));
			var tryGetValueMethod = AccessTools.Method(typeof(Dictionary<KeyCode, int>), nameof(Dictionary<KeyCode, int>.TryGetValue));

			var keyboardType = inputSystemAssembly.GetType("UnityEngine.InputSystem.Keyboard");
			var currentProperty = AccessTools.PropertyGetter(keyboardType, "current");
			var indexer = AccessTools.Method(keyboardType, "get_Item", new[] { keyEnum });

			var buttonControlType = inputSystemAssembly.GetType("UnityEngine.InputSystem.Controls.ButtonControl");

			GetButtonDelegate GenerateKeyGetter(string name, string property)
			{
				var targetProperty = AccessTools.PropertyGetter(buttonControlType, property);
				var dmd = new DynamicMethodDefinition(name, typeof(bool), new[] { typeof(KeyCode) });
				var il = dmd.GetILGenerator();
				var tmp = il.DeclareLocal(typeof(int));
				var defaultValueLabel = il.DefineLabel();

				il.Emit(OpCodes.Ldsfld, keyCodeToIndexField);
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldloca, tmp);
				il.Emit(OpCodes.Callvirt, tryGetValueMethod);
				il.Emit(OpCodes.Brfalse, defaultValueLabel);

				il.Emit(OpCodes.Call, currentProperty);
				il.Emit(OpCodes.Ldloc, tmp);
				il.Emit(OpCodes.Callvirt, indexer);
				il.Emit(OpCodes.Callvirt, targetProperty);
				il.Emit(OpCodes.Ret);

				il.MarkLabel(defaultValueLabel);
				il.Emit(OpCodes.Ldc_I4_0);
				il.Emit(OpCodes.Ret);

				return dmd.Generate().CreateDelegate<GetButtonDelegate>();
			}

			_getKey = GenerateKeyGetter("InputSystemGetKey", "isPressed");
			_getKeyDown = GenerateKeyGetter("InputSystemGetKeyDown", "wasPressedThisFrame");
			_getKeyUp = GenerateKeyGetter("InputSystemGetKeyUp", "wasReleasedThisFrame");

			var mouseType = inputSystemAssembly.GetType("UnityEngine.InputSystem.Mouse");
			var mouseCurrentProperty = AccessTools.PropertyGetter(mouseType, "current");
			var buttonProperties = new[]
			{
				"leftButton",
				"rightButton",
				"middleButton",
				"backButton",
				"forwardButton",
			};
			var buttonPropertyGetters = buttonProperties.Select(p => AccessTools.PropertyGetter(mouseType, p)).ToArray();

			GetButtonDelegate GenerateMouseGetter(string name, string property)
			{
				var targetProperty = AccessTools.PropertyGetter(buttonControlType, property);
				var dmd = new DynamicMethodDefinition(name, typeof(bool), new[] { typeof(KeyCode) });
				var il = dmd.GetILGenerator();

				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldc_I4, (int)KeyCode.Mouse0);
				il.Emit(OpCodes.Sub);

				var buttonSwitches = buttonPropertyGetters.Select(_ => il.DefineLabel()).ToArray();
				var defaultLabel = il.DefineLabel();
				il.Emit(OpCodes.Switch, buttonSwitches);
				il.Emit(OpCodes.Br, defaultLabel);

				for (var i = 0; i < buttonSwitches.Length; i++)
				{
					il.MarkLabel(buttonSwitches[i]);
					il.Emit(OpCodes.Call, mouseCurrentProperty);
					il.Emit(OpCodes.Callvirt, buttonPropertyGetters[i]);
					il.Emit(OpCodes.Callvirt, targetProperty);
					il.Emit(OpCodes.Ret);
				}

				il.MarkLabel(defaultLabel);
				il.Emit(OpCodes.Ldc_I4_0);
				il.Emit(OpCodes.Ret);

				return dmd.Generate().CreateDelegate<GetButtonDelegate>();
			}

			_getMouseButton = GenerateMouseGetter("InputSystemGetMouseButton", "isPressed");
			_getMouseButtonDown = GenerateMouseGetter("InputSystemGetMouseButtonDown", "wasPressedThisFrame");
			_getMouseButtonUp = GenerateMouseGetter("InputSystemGetMouseButtonUp", "wasReleasedThisFrame");

			GetMouseVectorDelegate GetPositionDelegate(string name, string property)
			{
				var targetProperty = AccessTools.PropertyGetter(mouseType, property);
				var readValueMethod = AccessTools.Method(targetProperty.ReturnType, "ReadValue");
				var dmd = new DynamicMethodDefinition(name, typeof(Vector2), Type.EmptyTypes);
				var il = dmd.GetILGenerator();

				il.Emit(OpCodes.Call, mouseCurrentProperty);
				il.Emit(OpCodes.Callvirt, targetProperty);
				il.Emit(OpCodes.Callvirt, readValueMethod);
				il.Emit(OpCodes.Ret);

				return dmd.Generate().CreateDelegate<GetMouseVectorDelegate>();
			}

			_getMousePosition = GetPositionDelegate("InputSystemGetMousePosition", "position");
			_getMouseScrollDelta = GetPositionDelegate("InputSystemGetMouseScrollDelta", "scroll");

			var mouseEnabledDmd = new DynamicMethodDefinition("InputSystemGetMouseEnabled", typeof(bool), Type.EmptyTypes);
			var mouseEnabledIl = mouseEnabledDmd.GetILGenerator();
			mouseEnabledIl.Emit(OpCodes.Call, mouseCurrentProperty);
			mouseEnabledIl.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(mouseType, "enabled"));
			mouseEnabledIl.Emit(OpCodes.Ret);

			_getMousePresent = mouseEnabledDmd.Generate().CreateDelegate<GetMouseStateDelegate>();

			var anyKeyProperty = AccessTools.PropertyGetter(keyboardType, "anyKey");

			GetMouseStateDelegate GetAnyKeyDelegate(string name, string property)
			{
				var targetProperty = AccessTools.PropertyGetter(buttonControlType, property);
				var dmd = new DynamicMethodDefinition(name, typeof(bool), Type.EmptyTypes);
				var il = dmd.GetILGenerator();

				il.Emit(OpCodes.Call, currentProperty);
				il.Emit(OpCodes.Callvirt, anyKeyProperty);
				il.Emit(OpCodes.Callvirt, targetProperty);
				il.Emit(OpCodes.Ret);

				return dmd.Generate().CreateDelegate<GetMouseStateDelegate>();
			}

			_getKeyboardAnyKeyIsPressed = GetAnyKeyDelegate("InputSystemGetKeyboardAnyKeyIsPressed", "isPressed");
			_getKeyboardAnyKeyWasPressedThisFrame = GetAnyKeyDelegate("InputSystemGetKeyboardAnyKeyWasPressedThisFrame", "wasPressedThisFrame");

			initialized = true;
		}

		private static GetButtonDelegate _getKey;
		public bool GetKey(string name) => _getKey(GetKeyCode(name));
		public bool GetKey(KeyCode key) => _getKey(key);

		private static GetButtonDelegate _getKeyDown;
		public bool GetKeyDown(string name) => _getKeyDown(GetKeyCode(name));
		public bool GetKeyDown(KeyCode key) => _getKeyDown(key);

		private static GetButtonDelegate _getKeyUp;
		public bool GetKeyUp(string name) => _getKeyUp(GetKeyCode(name));
		public bool GetKeyUp(KeyCode key) => _getKeyUp(key);

		private static GetButtonDelegate _getMouseButton;
		public bool GetMouseButton(int button) => _getMouseButton(KeyCode.Mouse0 + button);

		private static GetButtonDelegate _getMouseButtonDown;
		public bool GetMouseButtonDown(int button) => _getMouseButtonDown(KeyCode.Mouse0 + button);

		private static GetButtonDelegate _getMouseButtonUp;
		public bool GetMouseButtonUp(int button) => _getMouseButtonUp(KeyCode.Mouse0 + button);

		public void ResetInputAxes()
		{
			/*Not supported*/
		}

		private static GetMouseVectorDelegate _getMousePosition;
		public Vector3 mousePosition => _getMousePosition();

		private static GetMouseVectorDelegate _getMouseScrollDelta;
		public Vector2 mouseScrollDelta => _getMouseScrollDelta();

		private static GetMouseStateDelegate _getMousePresent;
		public bool mousePresent => _getMousePresent();

		private static GetMouseStateDelegate _getKeyboardAnyKeyIsPressed;

		public bool anyKey
		{
			get
			{
				if (_getKeyboardAnyKeyIsPressed())
					return true;
				return GetMouseButton(0) ||
					   GetMouseButton(1) ||
					   GetMouseButton(2) ||
					   GetMouseButton(3) ||
					   GetMouseButton(4);
			}
		}

		private static GetMouseStateDelegate _getKeyboardAnyKeyWasPressedThisFrame;

		public bool anyKeyDown
		{
			get
			{
				if (_getKeyboardAnyKeyWasPressedThisFrame())
					return true;
				return GetMouseButtonDown(0) ||
					   GetMouseButtonDown(1) ||
					   GetMouseButtonDown(2) ||
					   GetMouseButtonDown(3) ||
					   GetMouseButtonDown(4);
			}
		}

		public IEnumerable<KeyCode> SupportedKeyCodes => keyCodeToIndex.Keys.ToArray();

		private static KeyCode GetKeyCode(string name) => (KeyCode)Enum.Parse(typeof(KeyCode), name, true);

		private static bool TryEnumParse<T>(string name, out T val)
		{
			try
			{
				val = (T)Enum.Parse(typeof(T), name, true);
				return true;
			}
			catch (Exception)
			{
				val = default;
				return false;
			}
		}
	}

	internal class LegacyInputSystem : IInputSystem
	{
		public bool GetKey(string name)
		{
			return Input.GetKey(name);
		}

		public bool GetKey(KeyCode key)
		{
			return Input.GetKey(key);
		}

		public bool GetKeyDown(string name)
		{
			return Input.GetKeyDown(name);
		}

		public bool GetKeyDown(KeyCode key)
		{
			return Input.GetKeyDown(key);
		}

		public bool GetKeyUp(string name)
		{
			return Input.GetKeyUp(name);
		}

		public bool GetKeyUp(KeyCode key)
		{
			return Input.GetKeyUp(key);
		}

		public bool GetMouseButton(int button)
		{
			return Input.GetMouseButton(button);
		}

		public bool GetMouseButtonDown(int button)
		{
			return Input.GetMouseButtonDown(button);
		}

		public bool GetMouseButtonUp(int button)
		{
			return Input.GetMouseButtonUp(button);
		}

		public void ResetInputAxes()
		{
			Input.ResetInputAxes();
		}

		public Vector3 mousePosition => Input.mousePosition;
		public Vector2 mouseScrollDelta => Input.mouseScrollDelta;
		public bool mousePresent => Input.mousePresent;
		public bool anyKey => Input.anyKey;
		public bool anyKeyDown => Input.anyKeyDown;

		public IEnumerable<KeyCode> SupportedKeyCodes { get; } = (KeyCode[])Enum.GetValues(typeof(KeyCode));
	}

	internal class NullInputSystem : IInputSystem
	{
		private static readonly KeyCode[] NoKeys = new KeyCode[0];
		public Vector3 mousePosition => Vector3.zero;
		public Vector2 mouseScrollDelta => Vector2.zero;
		public bool mousePresent => false;
		public bool anyKey => false;
		public bool anyKeyDown => false;
		public IEnumerable<KeyCode> SupportedKeyCodes => NoKeys;
		public bool GetKey(string name) => false;
		public bool GetKey(KeyCode key) => false;
		public bool GetKeyDown(string name) => false;
		public bool GetKeyDown(KeyCode key) => false;
		public bool GetKeyUp(string name) => false;
		public bool GetKeyUp(KeyCode key) => false;
		public bool GetMouseButton(int button) => false;
		public bool GetMouseButtonDown(int button) => false;
		public bool GetMouseButtonUp(int button) => false;
		public void ResetInputAxes() { }
	}
}