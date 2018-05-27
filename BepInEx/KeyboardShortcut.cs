using System;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace BepInEx
{
    /// <summary>
    /// A keyboard shortcut that can be used in Update method to check if user presses a key combo.
    /// Modifier keys are handled properly - if your keybind is Space, and Space+Shift is pressed, your keybind will not fire.
    /// Use SavedKeyboardShortcut version to automatically save any changes made to this keybind.
    ///
    /// How to use: Use IsDown instead of the Imput.GetKeyDown in the Update loop.
    /// </summary>
    public class KeyboardShortcut : INotifyPropertyChanged
    {
        private bool alt;

        private bool control;

        private KeyCode key;

        private bool shift;

        /// <summary>
        /// Create a new keyboard shortcut.
        /// </summary>
        /// <param name="key">Main key to press</param>
        /// <param name="control">Should Control be held down?</param>
        /// <param name="alt">Should Alt be held down?</param>
        /// <param name="shift">Should Shift be held down?</param>
        public KeyboardShortcut(KeyCode key, bool control = false, bool alt = false, bool shift = false)
        {
            Key = key;
            Control = control;
            Alt = alt;
            Shift = shift;
        }

        public KeyboardShortcut()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool Alt
        {
            get
            {
                return alt;
            }

            set
            {
                alt = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Alt)));
            }
        }

        public bool Control
        {
            get
            {
                return control;
            }

            set
            {
                control = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Control)));
            }
        }

        public KeyCode Key
        {
            get
            {
                return key;
            }

            set
            {
                key = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
            }
        }

        public bool Shift
        {
            get
            {
                return shift;
            }

            set
            {
                shift = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shift)));
            }
        }

        public static KeyboardShortcut Deserialize(string str)
        {
            try
            {
                var parts = str.Split(' ').Select(x => int.Parse(x)).ToArray();
                return new KeyboardShortcut((KeyCode)parts[0], parts[1] == 1, parts[2] == 1, parts[3] == 1);
            }
            catch (SystemException ex)
            {
                BepInLogger.Log("Failed to read keybind from settings: " + ex.Message);
                return null;
            }
        }

        public string Serialize()
        {
            return $"{(int)Key} {(Control ? 1 : 0)} {(Alt ? 1 : 0)} {(Shift ? 1 : 0)}";
        }

        /// <summary>
        /// Check if the main key was just pressed (Input.GetKeyDown), and specified modifier keys are all pressed
        /// </summary>
        public bool IsDown()
        {
            return Input.GetKeyDown(Key) && ModifierKeyTest();
        }

        /// <summary>
        /// Check if the main key is currently held down (Input.GetKeyDown), and specified modifier keys are all pressed
        /// </summary>
        public bool IsPressed()
        {
            return Input.GetKeyDown(Key) && ModifierKeyTest();
        }

        /// <summary>
        /// Check if the main key was just lifted (Input.GetKeyUp), and specified modifier keys are all pressed.
        /// </summary>
        public bool IsUp()
        {
            return Input.GetKeyUp(Key) && ModifierKeyTest();
        }

        private bool ModifierKeyTest()
        {
            if (Key == KeyCode.None)
                return false;

            if (Control && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                return false;

            if (Alt && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                return false;

            if (Shift && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                return false;

            return true;
        }
    }
}