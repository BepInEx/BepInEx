using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// ReSharper disable InconsistentNaming

namespace BepInEx
{
    /// <summary>
    /// Abstraction layer over Unity's input systems for use in universal plugins that need to use hotkeys.
    /// It can use either Input or Unity.InputSystem, depending on what's available. Input is preferred.
    /// WARNING: Use only inside of Unity's main thread!
    /// </summary>
    public class UnityInput
    {
        private static IInputSystem current;
        /// <summary>
        /// Best currently supported input system.
        /// </summary>
        public static IInputSystem Current
        {
            get
            {
                if (current == null)
                {
                    try
                    {
                        Input.GetKeyDown(KeyCode.A);
                        current = new LegacyInputSystem();
                        Logging.Logger.Log(LogLevel.Debug, "[UnityInput] Using LegacyInputSystem");
                    }
                    catch (InvalidOperationException)
                    {
                        current = new NewInputSystem();
                        Logging.Logger.Log(LogLevel.Debug, "[UnityInput] Using NewInputSystem");
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Log(LogLevel.Warning, "[UnityInput] Failed to detect available input systems - " + ex);
                    }
                }
                return current;
            }
        }

        /// <summary>
        /// True if the Input class is not disabled.
        /// </summary>
        public bool LegacyInputSystemAvailable => Current is LegacyInputSystem;
    }

    /// <summary>
    /// Generic input system interface. Just barely good enough for hotkeys.
    /// </summary>
    public interface IInputSystem
    {
        // No easy way to use these with the new input system
        //bool GetButton(string buttonName);
        //bool GetButtonDown(string buttonName);
        //bool GetButtonUp(string buttonName);

        /// <inheritdoc cref="Input.GetKey(string)"/>
        bool GetKey(string name);
        /// <inheritdoc cref="Input.GetKey(KeyCode)"/>
        bool GetKey(KeyCode key);

        /// <inheritdoc cref="Input.GetKeyDown(string)"/>
        bool GetKeyDown(string name);
        /// <inheritdoc cref="Input.GetKeyDown(KeyCode)"/>
        bool GetKeyDown(KeyCode key);

        /// <inheritdoc cref="Input.GetKeyUp(string)"/>
        bool GetKeyUp(string name);
        /// <inheritdoc cref="Input.GetKeyUp(KeyCode)"/>
        bool GetKeyUp(KeyCode key);


        /// <inheritdoc cref="Input.GetMouseButton(int)"/>
        bool GetMouseButton(int button);
        /// <inheritdoc cref="Input.GetMouseButtonDown(int)"/>
        bool GetMouseButtonDown(int button);
        /// <inheritdoc cref="Input.GetMouseButtonUp(int)"/>
        bool GetMouseButtonUp(int button);

        /// <inheritdoc cref="Input.ResetInputAxes()"/>
        void ResetInputAxes();

        /// <inheritdoc cref="Input.mousePosition"/>
        Vector3 mousePosition { get; }
        /// <inheritdoc cref="Input.mouseScrollDelta"/>
        Vector2 mouseScrollDelta { get; }

        /// <inheritdoc cref="Input.mousePresent"/>
        bool mousePresent { get; }

        /// <inheritdoc cref="Input.anyKey"/>
        bool anyKey { get; }
        /// <inheritdoc cref="Input.anyKeyDown"/>
        bool anyKeyDown { get; }

        /// <summary>
        /// All KeyCodes supported by the current input system.
        /// </summary>
        IEnumerable<KeyCode> SupportedKeyCodes { get; }
    }

    internal class NewInputSystem : IInputSystem
    {
        public bool GetKey(string name) => GetControl(name)?.isPressed ?? false;

        public bool GetKey(KeyCode key) => GetControl(key)?.isPressed ?? false;

        public bool GetKeyDown(string name) => GetControl(name)?.wasPressedThisFrame ?? false;

        public bool GetKeyDown(KeyCode key) => GetControl(key)?.wasPressedThisFrame ?? false;

        public bool GetKeyUp(string name) => GetControl(name)?.wasReleasedThisFrame ?? false;

        public bool GetKeyUp(KeyCode key) => GetControl(key)?.wasReleasedThisFrame ?? false;

        public bool GetMouseButton(int button) => GetControl(KeyCode.Mouse0 + button)?.isPressed ?? false;

        public bool GetMouseButtonDown(int button) => GetControl(KeyCode.Mouse0 + button)?.wasPressedThisFrame ?? false;

        public bool GetMouseButtonUp(int button) => GetControl(KeyCode.Mouse0 + button)?.wasReleasedThisFrame ?? false;

        public void ResetInputAxes() { /*Not supported*/ }

        public Vector3 mousePosition => Mouse.current.position.ReadValue();
        public Vector2 mouseScrollDelta => Mouse.current.scroll.ReadValue();
        public bool mousePresent => Mouse.current.enabled;

        public bool anyKey
        {
            get
            {
                if (Keyboard.current.anyKey.isPressed)
                    return true;
                var current = Mouse.current;
                return current.leftButton.isPressed ||
                       current.rightButton.isPressed ||
                       current.forwardButton.isPressed ||
                       current.backButton.isPressed ||
                       current.middleButton.isPressed;
            }
        }

        public bool anyKeyDown
        {
            get
            {
                if (Keyboard.current.anyKey.wasPressedThisFrame)
                    return true;
                var current = Mouse.current;
                return current.leftButton.wasPressedThisFrame ||
                       current.rightButton.wasPressedThisFrame ||
                       current.forwardButton.wasPressedThisFrame ||
                       current.backButton.wasPressedThisFrame ||
                       current.middleButton.wasPressedThisFrame;
            }
        }

        public IEnumerable<KeyCode> SupportedKeyCodes { get; } = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Where(x => GetControl(x, true) != null).ToList();

        private static ButtonControl GetControl(string name) => GetControl((KeyCode)Enum.Parse(typeof(KeyCode), name, true));

        private static ButtonControl GetControl(KeyCode key, bool silent = false)
        {
            switch (key)
            {
                case KeyCode.None:
                    break;
                case KeyCode.Backspace:
                    return Keyboard.current.backspaceKey;
                case KeyCode.Delete:
                    return Keyboard.current.deleteKey;
                case KeyCode.Tab:
                    return Keyboard.current.tabKey;
                case KeyCode.Clear:
                    break;
                case KeyCode.Return:
                    return Keyboard.current.enterKey;
                case KeyCode.Pause:
                    return Keyboard.current.pauseKey;
                case KeyCode.Escape:
                    return Keyboard.current.escapeKey;
                case KeyCode.Space:
                    return Keyboard.current.spaceKey;
                case KeyCode.Keypad0:
                    return Keyboard.current.numpad0Key;
                case KeyCode.Keypad1:
                    return Keyboard.current.numpad1Key;
                case KeyCode.Keypad2:
                    return Keyboard.current.numpad2Key;
                case KeyCode.Keypad3:
                    return Keyboard.current.numpad3Key;
                case KeyCode.Keypad4:
                    return Keyboard.current.numpad4Key;
                case KeyCode.Keypad5:
                    return Keyboard.current.numpad5Key;
                case KeyCode.Keypad6:
                    return Keyboard.current.numpad6Key;
                case KeyCode.Keypad7:
                    return Keyboard.current.numpad7Key;
                case KeyCode.Keypad8:
                    return Keyboard.current.numpad8Key;
                case KeyCode.Keypad9:
                    return Keyboard.current.numpad9Key;
                case KeyCode.KeypadPeriod:
                    return Keyboard.current.numpadPeriodKey;
                case KeyCode.KeypadDivide:
                    return Keyboard.current.numpadDivideKey;
                case KeyCode.KeypadMultiply:
                    return Keyboard.current.numpadMultiplyKey;
                case KeyCode.KeypadMinus:
                    return Keyboard.current.numpadMinusKey;
                case KeyCode.KeypadPlus:
                    return Keyboard.current.numpadPlusKey;
                case KeyCode.KeypadEnter:
                    return Keyboard.current.numpadEnterKey;
                case KeyCode.KeypadEquals:
                    return Keyboard.current.numpadEqualsKey;
                case KeyCode.UpArrow:
                    return Keyboard.current.upArrowKey;
                case KeyCode.DownArrow:
                    return Keyboard.current.downArrowKey;
                case KeyCode.RightArrow:
                    return Keyboard.current.rightArrowKey;
                case KeyCode.LeftArrow:
                    return Keyboard.current.leftArrowKey;
                case KeyCode.Insert:
                    return Keyboard.current.insertKey;
                case KeyCode.Home:
                    return Keyboard.current.homeKey;
                case KeyCode.End:
                    return Keyboard.current.endKey;
                case KeyCode.PageUp:
                    return Keyboard.current.pageUpKey;
                case KeyCode.PageDown:
                    return Keyboard.current.pageDownKey;
                case KeyCode.F1:
                    return Keyboard.current.f1Key;
                case KeyCode.F2:
                    return Keyboard.current.f2Key;
                case KeyCode.F3:
                    return Keyboard.current.f3Key;
                case KeyCode.F4:
                    return Keyboard.current.f4Key;
                case KeyCode.F5:
                    return Keyboard.current.f5Key;
                case KeyCode.F6:
                    return Keyboard.current.f6Key;
                case KeyCode.F7:
                    return Keyboard.current.f7Key;
                case KeyCode.F8:
                    return Keyboard.current.f8Key;
                case KeyCode.F9:
                    return Keyboard.current.f9Key;
                case KeyCode.F10:
                    return Keyboard.current.f10Key;
                case KeyCode.F11:
                    return Keyboard.current.f11Key;
                case KeyCode.F12:
                    return Keyboard.current.f12Key;
                case KeyCode.F13:
                    break;
                case KeyCode.F14:
                    break;
                case KeyCode.F15:
                    break;
                case KeyCode.Alpha0:
                    return Keyboard.current.digit0Key;
                case KeyCode.Alpha1:
                    return Keyboard.current.digit1Key;
                case KeyCode.Alpha2:
                    return Keyboard.current.digit2Key;
                case KeyCode.Alpha3:
                    return Keyboard.current.digit3Key;
                case KeyCode.Alpha4:
                    return Keyboard.current.digit4Key;
                case KeyCode.Alpha5:
                    return Keyboard.current.digit5Key;
                case KeyCode.Alpha6:
                    return Keyboard.current.digit6Key;
                case KeyCode.Alpha7:
                    return Keyboard.current.digit7Key;
                case KeyCode.Alpha8:
                    return Keyboard.current.digit8Key;
                case KeyCode.Alpha9:
                    return Keyboard.current.digit9Key;
                case KeyCode.Exclaim:
                    break;
                case KeyCode.DoubleQuote:
                    break;
                case KeyCode.Hash:
                    break;
                case KeyCode.Dollar:
                    break;
                case KeyCode.Ampersand:
                    break;
                case KeyCode.Quote:
                    return Keyboard.current.quoteKey;
                case KeyCode.LeftParen:
                    break;
                case KeyCode.RightParen:
                    break;
                case KeyCode.Asterisk:
                    break;
                case KeyCode.Plus:
                    return Keyboard.current.numpadPlusKey;
                case KeyCode.Comma:
                    return Keyboard.current.commaKey;
                case KeyCode.Minus:
                    return Keyboard.current.minusKey;
                case KeyCode.Period:
                    return Keyboard.current.periodKey;
                case KeyCode.Slash:
                    return Keyboard.current.slashKey;
                case KeyCode.Colon:
                    break;
                case KeyCode.Semicolon:
                    return Keyboard.current.semicolonKey;
                case KeyCode.Less:
                    break;
                case KeyCode.Equals:
                    return Keyboard.current.equalsKey;
                case KeyCode.Greater:
                    break;
                case KeyCode.Question:
                    break;
                case KeyCode.At:
                    break;
                case KeyCode.LeftBracket:
                    return Keyboard.current.leftBracketKey;
                case KeyCode.Backslash:
                    return Keyboard.current.backslashKey;
                case KeyCode.RightBracket:
                    return Keyboard.current.rightBracketKey;
                case KeyCode.Caret:
                    break;
                case KeyCode.Underscore:
                    break;
                case KeyCode.BackQuote:
                    return Keyboard.current.backquoteKey;
                case KeyCode.A:
                    return Keyboard.current.aKey;
                case KeyCode.B:
                    return Keyboard.current.bKey;
                case KeyCode.C:
                    return Keyboard.current.cKey;
                case KeyCode.D:
                    return Keyboard.current.dKey;
                case KeyCode.E:
                    return Keyboard.current.eKey;
                case KeyCode.F:
                    return Keyboard.current.fKey;
                case KeyCode.G:
                    return Keyboard.current.gKey;
                case KeyCode.H:
                    return Keyboard.current.hKey;
                case KeyCode.I:
                    return Keyboard.current.iKey;
                case KeyCode.J:
                    return Keyboard.current.jKey;
                case KeyCode.K:
                    return Keyboard.current.kKey;
                case KeyCode.L:
                    return Keyboard.current.lKey;
                case KeyCode.M:
                    return Keyboard.current.mKey;
                case KeyCode.N:
                    return Keyboard.current.nKey;
                case KeyCode.O:
                    return Keyboard.current.oKey;
                case KeyCode.P:
                    return Keyboard.current.pKey;
                case KeyCode.Q:
                    return Keyboard.current.qKey;
                case KeyCode.R:
                    return Keyboard.current.rKey;
                case KeyCode.S:
                    return Keyboard.current.sKey;
                case KeyCode.T:
                    return Keyboard.current.tKey;
                case KeyCode.U:
                    return Keyboard.current.uKey;
                case KeyCode.V:
                    return Keyboard.current.vKey;
                case KeyCode.W:
                    return Keyboard.current.wKey;
                case KeyCode.X:
                    return Keyboard.current.xKey;
                case KeyCode.Y:
                    return Keyboard.current.yKey;
                case KeyCode.Z:
                    return Keyboard.current.zKey;
                case KeyCode.Numlock:
                    return Keyboard.current.numLockKey;
                case KeyCode.CapsLock:
                    return Keyboard.current.capsLockKey;
                case KeyCode.ScrollLock:
                    return Keyboard.current.scrollLockKey;
                case KeyCode.RightShift:
                    return Keyboard.current.rightShiftKey;
                case KeyCode.LeftShift:
                    return Keyboard.current.leftShiftKey;
                case KeyCode.RightControl:
                    return Keyboard.current.rightCtrlKey;
                case KeyCode.LeftControl:
                    return Keyboard.current.leftCtrlKey;
                case KeyCode.RightAlt:
                    return Keyboard.current.rightAltKey;
                case KeyCode.LeftAlt:
                    return Keyboard.current.leftAltKey;
                case KeyCode.LeftCommand:
                    return Keyboard.current.leftCommandKey;
                case KeyCode.LeftWindows:
                    return Keyboard.current.leftWindowsKey;
                case KeyCode.RightCommand:
                    return Keyboard.current.rightCommandKey;
                case KeyCode.RightWindows:
                    return Keyboard.current.rightWindowsKey;
                case KeyCode.AltGr:
                    break;
                case KeyCode.Help:
                    break;
                case KeyCode.Print:
                    return Keyboard.current.printScreenKey;
                case KeyCode.SysReq:
                    break;
                case KeyCode.Break:
                    break;
                case KeyCode.Menu:
                    return Keyboard.current.contextMenuKey;
                case KeyCode.Mouse0:
                    return Mouse.current.leftButton;
                case KeyCode.Mouse1:
                    return Mouse.current.rightButton;
                case KeyCode.Mouse2:
                    return Mouse.current.middleButton;
                case KeyCode.Mouse3:
                    return Mouse.current.backButton;
                case KeyCode.Mouse4:
                    return Mouse.current.forwardButton;
                case KeyCode.Mouse5:
                    break;
                case KeyCode.Mouse6:
                    break;
                case KeyCode.JoystickButton0:
                    break;
                case KeyCode.JoystickButton1:
                    break;
                case KeyCode.JoystickButton2:
                    break;
                case KeyCode.JoystickButton3:
                    break;
                case KeyCode.JoystickButton4:
                    break;
                case KeyCode.JoystickButton5:
                    break;
                case KeyCode.JoystickButton6:
                    break;
                case KeyCode.JoystickButton7:
                    break;
                case KeyCode.JoystickButton8:
                    break;
                case KeyCode.JoystickButton9:
                    break;
                case KeyCode.JoystickButton10:
                    break;
                case KeyCode.JoystickButton11:
                    break;
                case KeyCode.JoystickButton12:
                    break;
                case KeyCode.JoystickButton13:
                    break;
                case KeyCode.JoystickButton14:
                    break;
                case KeyCode.JoystickButton15:
                    break;
                case KeyCode.JoystickButton16:
                    break;
                case KeyCode.JoystickButton17:
                    break;
                case KeyCode.JoystickButton18:
                    break;
                case KeyCode.JoystickButton19:
                    break;
                case KeyCode.Joystick1Button0:
                    break;
                case KeyCode.Joystick1Button1:
                    break;
                case KeyCode.Joystick1Button2:
                    break;
                case KeyCode.Joystick1Button3:
                    break;
                case KeyCode.Joystick1Button4:
                    break;
                case KeyCode.Joystick1Button5:
                    break;
                case KeyCode.Joystick1Button6:
                    break;
                case KeyCode.Joystick1Button7:
                    break;
                case KeyCode.Joystick1Button8:
                    break;
                case KeyCode.Joystick1Button9:
                    break;
                case KeyCode.Joystick1Button10:
                    break;
                case KeyCode.Joystick1Button11:
                    break;
                case KeyCode.Joystick1Button12:
                    break;
                case KeyCode.Joystick1Button13:
                    break;
                case KeyCode.Joystick1Button14:
                    break;
                case KeyCode.Joystick1Button15:
                    break;
                case KeyCode.Joystick1Button16:
                    break;
                case KeyCode.Joystick1Button17:
                    break;
                case KeyCode.Joystick1Button18:
                    break;
                case KeyCode.Joystick1Button19:
                    break;
                case KeyCode.Joystick2Button0:
                    break;
                case KeyCode.Joystick2Button1:
                    break;
                case KeyCode.Joystick2Button2:
                    break;
                case KeyCode.Joystick2Button3:
                    break;
                case KeyCode.Joystick2Button4:
                    break;
                case KeyCode.Joystick2Button5:
                    break;
                case KeyCode.Joystick2Button6:
                    break;
                case KeyCode.Joystick2Button7:
                    break;
                case KeyCode.Joystick2Button8:
                    break;
                case KeyCode.Joystick2Button9:
                    break;
                case KeyCode.Joystick2Button10:
                    break;
                case KeyCode.Joystick2Button11:
                    break;
                case KeyCode.Joystick2Button12:
                    break;
                case KeyCode.Joystick2Button13:
                    break;
                case KeyCode.Joystick2Button14:
                    break;
                case KeyCode.Joystick2Button15:
                    break;
                case KeyCode.Joystick2Button16:
                    break;
                case KeyCode.Joystick2Button17:
                    break;
                case KeyCode.Joystick2Button18:
                    break;
                case KeyCode.Joystick2Button19:
                    break;
                case KeyCode.Joystick3Button0:
                    break;
                case KeyCode.Joystick3Button1:
                    break;
                case KeyCode.Joystick3Button2:
                    break;
                case KeyCode.Joystick3Button3:
                    break;
                case KeyCode.Joystick3Button4:
                    break;
                case KeyCode.Joystick3Button5:
                    break;
                case KeyCode.Joystick3Button6:
                    break;
                case KeyCode.Joystick3Button7:
                    break;
                case KeyCode.Joystick3Button8:
                    break;
                case KeyCode.Joystick3Button9:
                    break;
                case KeyCode.Joystick3Button10:
                    break;
                case KeyCode.Joystick3Button11:
                    break;
                case KeyCode.Joystick3Button12:
                    break;
                case KeyCode.Joystick3Button13:
                    break;
                case KeyCode.Joystick3Button14:
                    break;
                case KeyCode.Joystick3Button15:
                    break;
                case KeyCode.Joystick3Button16:
                    break;
                case KeyCode.Joystick3Button17:
                    break;
                case KeyCode.Joystick3Button18:
                    break;
                case KeyCode.Joystick3Button19:
                    break;
                case KeyCode.Joystick4Button0:
                    break;
                case KeyCode.Joystick4Button1:
                    break;
                case KeyCode.Joystick4Button2:
                    break;
                case KeyCode.Joystick4Button3:
                    break;
                case KeyCode.Joystick4Button4:
                    break;
                case KeyCode.Joystick4Button5:
                    break;
                case KeyCode.Joystick4Button6:
                    break;
                case KeyCode.Joystick4Button7:
                    break;
                case KeyCode.Joystick4Button8:
                    break;
                case KeyCode.Joystick4Button9:
                    break;
                case KeyCode.Joystick4Button10:
                    break;
                case KeyCode.Joystick4Button11:
                    break;
                case KeyCode.Joystick4Button12:
                    break;
                case KeyCode.Joystick4Button13:
                    break;
                case KeyCode.Joystick4Button14:
                    break;
                case KeyCode.Joystick4Button15:
                    break;
                case KeyCode.Joystick4Button16:
                    break;
                case KeyCode.Joystick4Button17:
                    break;
                case KeyCode.Joystick4Button18:
                    break;
                case KeyCode.Joystick4Button19:
                    break;
                case KeyCode.Joystick5Button0:
                    break;
                case KeyCode.Joystick5Button1:
                    break;
                case KeyCode.Joystick5Button2:
                    break;
                case KeyCode.Joystick5Button3:
                    break;
                case KeyCode.Joystick5Button4:
                    break;
                case KeyCode.Joystick5Button5:
                    break;
                case KeyCode.Joystick5Button6:
                    break;
                case KeyCode.Joystick5Button7:
                    break;
                case KeyCode.Joystick5Button8:
                    break;
                case KeyCode.Joystick5Button9:
                    break;
                case KeyCode.Joystick5Button10:
                    break;
                case KeyCode.Joystick5Button11:
                    break;
                case KeyCode.Joystick5Button12:
                    break;
                case KeyCode.Joystick5Button13:
                    break;
                case KeyCode.Joystick5Button14:
                    break;
                case KeyCode.Joystick5Button15:
                    break;
                case KeyCode.Joystick5Button16:
                    break;
                case KeyCode.Joystick5Button17:
                    break;
                case KeyCode.Joystick5Button18:
                    break;
                case KeyCode.Joystick5Button19:
                    break;
                case KeyCode.Joystick6Button0:
                    break;
                case KeyCode.Joystick6Button1:
                    break;
                case KeyCode.Joystick6Button2:
                    break;
                case KeyCode.Joystick6Button3:
                    break;
                case KeyCode.Joystick6Button4:
                    break;
                case KeyCode.Joystick6Button5:
                    break;
                case KeyCode.Joystick6Button6:
                    break;
                case KeyCode.Joystick6Button7:
                    break;
                case KeyCode.Joystick6Button8:
                    break;
                case KeyCode.Joystick6Button9:
                    break;
                case KeyCode.Joystick6Button10:
                    break;
                case KeyCode.Joystick6Button11:
                    break;
                case KeyCode.Joystick6Button12:
                    break;
                case KeyCode.Joystick6Button13:
                    break;
                case KeyCode.Joystick6Button14:
                    break;
                case KeyCode.Joystick6Button15:
                    break;
                case KeyCode.Joystick6Button16:
                    break;
                case KeyCode.Joystick6Button17:
                    break;
                case KeyCode.Joystick6Button18:
                    break;
                case KeyCode.Joystick6Button19:
                    break;
                case KeyCode.Joystick7Button0:
                    break;
                case KeyCode.Joystick7Button1:
                    break;
                case KeyCode.Joystick7Button2:
                    break;
                case KeyCode.Joystick7Button3:
                    break;
                case KeyCode.Joystick7Button4:
                    break;
                case KeyCode.Joystick7Button5:
                    break;
                case KeyCode.Joystick7Button6:
                    break;
                case KeyCode.Joystick7Button7:
                    break;
                case KeyCode.Joystick7Button8:
                    break;
                case KeyCode.Joystick7Button9:
                    break;
                case KeyCode.Joystick7Button10:
                    break;
                case KeyCode.Joystick7Button11:
                    break;
                case KeyCode.Joystick7Button12:
                    break;
                case KeyCode.Joystick7Button13:
                    break;
                case KeyCode.Joystick7Button14:
                    break;
                case KeyCode.Joystick7Button15:
                    break;
                case KeyCode.Joystick7Button16:
                    break;
                case KeyCode.Joystick7Button17:
                    break;
                case KeyCode.Joystick7Button18:
                    break;
                case KeyCode.Joystick7Button19:
                    break;
                case KeyCode.Joystick8Button0:
                    break;
                case KeyCode.Joystick8Button1:
                    break;
                case KeyCode.Joystick8Button2:
                    break;
                case KeyCode.Joystick8Button3:
                    break;
                case KeyCode.Joystick8Button4:
                    break;
                case KeyCode.Joystick8Button5:
                    break;
                case KeyCode.Joystick8Button6:
                    break;
                case KeyCode.Joystick8Button7:
                    break;
                case KeyCode.Joystick8Button8:
                    break;
                case KeyCode.Joystick8Button9:
                    break;
                case KeyCode.Joystick8Button10:
                    break;
                case KeyCode.Joystick8Button11:
                    break;
                case KeyCode.Joystick8Button12:
                    break;
                case KeyCode.Joystick8Button13:
                    break;
                case KeyCode.Joystick8Button14:
                    break;
                case KeyCode.Joystick8Button15:
                    break;
                case KeyCode.Joystick8Button16:
                    break;
                case KeyCode.Joystick8Button17:
                    break;
                case KeyCode.Joystick8Button18:
                    break;
                case KeyCode.Joystick8Button19:
                    break;
                //default:
                //    throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }

            if (!silent)
                Logging.Logger.Log(LogLevel.Debug, $"[{nameof(NewInputSystem)}] Unsupported key: {key}");

            return null;
        }
    }

    internal class LegacyInputSystem : IInputSystem
    {
        public bool GetKey(string name) => Input.GetKey(name);

        public bool GetKey(KeyCode key) => Input.GetKey(key);

        public bool GetKeyDown(string name) => Input.GetKeyDown(name);

        public bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);

        public bool GetKeyUp(string name) => Input.GetKeyUp(name);

        public bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

        public bool GetMouseButton(int button) => Input.GetMouseButton(button);

        public bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);

        public bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);

        public void ResetInputAxes() => Input.ResetInputAxes();

        public Vector3 mousePosition => Input.mousePosition;
        public Vector2 mouseScrollDelta => Input.mouseScrollDelta;
        public bool mousePresent => Input.mousePresent;
        public bool anyKey => Input.anyKey;
        public bool anyKeyDown => Input.anyKeyDown;

        public IEnumerable<KeyCode> SupportedKeyCodes { get; } = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    }
}
