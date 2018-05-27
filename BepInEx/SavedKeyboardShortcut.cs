namespace BepInEx
{
    /// <summary>
    /// A keyboard shortcut that can be used in Update method to check if user presses a key combo.
    /// Modifier keys are handled properly - if your keybind is Space, and Space+Shift is pressed, your keybind will not fire.
    /// This wrapper automatically saves any changes made to the config file.
    ///
    /// How to use: Use IsDown instead of the Imput.GetKeyDown in the Update loop.
    /// </summary>
    public class SavedKeyboardShortcut : ConfigWrapper<KeyboardShortcut>
    {
        public SavedKeyboardShortcut(string name, BaseUnityPlugin plugin, KeyboardShortcut defaultShortcut)
            : base(name, plugin, KeyboardShortcut.Deserialize, k => k.Serialize(), defaultShortcut)
        {
        }

        public SavedKeyboardShortcut(string name, string section, KeyboardShortcut defaultShortcut)
            : base(name, section, KeyboardShortcut.Deserialize, k => k.Serialize(), defaultShortcut)
        {
        }

        private KeyboardShortcut _last;

        private void SetNewLast(KeyboardShortcut value)
        {
            if (_last != null)
                _last.PropertyChanged -= ShortcutChanged;

            _last = value;
            _last.PropertyChanged += ShortcutChanged;
        }

        protected override void SetValue(KeyboardShortcut value)
        {
            SetNewLast(value);
            base.SetValue(value);
        }
        
        protected override KeyboardShortcut GetValue()
        {
            var value = base.GetValue();
            SetNewLast(value);
            return value;
        }

        private void ShortcutChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.SetValue((KeyboardShortcut)sender);
        }

        /// <summary>
        /// Check if the main key is currently held down (Input.GetKey), and specified modifier keys are all pressed
        /// </summary>
        public bool IsPressed()
        {
            return Value.IsPressed();
        }

        /// <summary>
        /// Check if the main key was just pressed (Input.GetKeyDown), and specified modifier keys are all pressed
        /// </summary>
        public bool IsDown()
        {
            return Value.IsDown();
        }

        /// <summary>
        /// Check if the main key was just lifted (Input.GetKeyUp), and specified modifier keys are all pressed.
        /// </summary>
        public bool IsUp()
        {
            return Value.IsUp();
        }
    }
}
