using System;
using System.ComponentModel;
using BepInEx.Logging;

namespace BepInEx
{
    public interface IConfigConverter<T>
    {
        string ConvertToString(T value);
        T ConvertFromString(string str);
    }
    
    public class ConfigWrapper<T>
    {
        private readonly Func<string, T> _strToObj;
        private readonly Func<T, string> _objToStr;
        private readonly string _defaultStr;
        private readonly T _default;
        private T _lastValue;
        private bool _lastValueSet;

        public string Key { get; protected set; }

        public string Section { get; protected set; }

        public T Value
        {
            get { return GetValue(); }
            set { SetValue(value); }
        }

        public ConfigWrapper(string key, T @default = default(T))
        {
            var cvt = TypeDescriptor.GetConverter(typeof(T));

            if (!cvt.CanConvertFrom(typeof(string)))
                throw new ArgumentException("Default TypeConverter can't convert from String");

            if (!cvt.CanConvertTo(typeof(string)))
                throw new ArgumentException("Default TypeConverter can't convert to String");

            _strToObj = (str) => (T)cvt.ConvertFromInvariantString(str);
            _objToStr = (obj) => cvt.ConvertToInvariantString(obj);

            _defaultStr = _objToStr(@default);
            _default = @default;
            Key = key;
        }

        public ConfigWrapper(string key, Func<string, T> strToObj, Func<T, string> objToStr, T @default = default(T))
        {
            if (objToStr == null)
                throw new ArgumentNullException("objToStr");

            if (strToObj == null)
                throw new ArgumentNullException("strToObj");

            _strToObj = strToObj;
            _objToStr = objToStr;

            _defaultStr = _objToStr(@default);
            Key = key;
        }

        public ConfigWrapper(string key, IConfigConverter<T> converter, T @default = default(T))
            : this(key, converter.ConvertFromString, converter.ConvertToString, @default)
        {

        }


        public ConfigWrapper(string key, BaseUnityPlugin plugin, T @default = default(T))
            : this(key, @default)
        {
            Section = MetadataHelper.GetMetadata(plugin).GUID;
        }

        public ConfigWrapper(string key, BaseUnityPlugin plugin, Func<string, T> strToObj, Func<T, string> objToStr, T @default = default(T))
          : this(key, strToObj, objToStr, @default)
        {
            Section = MetadataHelper.GetMetadata(plugin).GUID;
        }

        public ConfigWrapper(string key, BaseUnityPlugin plugin, IConfigConverter<T> converter, T @default = default(T))
          : this(key, converter.ConvertFromString, converter.ConvertToString, @default)
        {
            Section = MetadataHelper.GetMetadata(plugin).GUID;
        }

        public ConfigWrapper(string key, string section, T @default = default(T))
            : this(key, @default)
        {
            Section = section;
        }

        public ConfigWrapper(string key, string section, Func<string, T> strToObj, Func<T, string> objToStr, T @default = default(T))
           : this(key, strToObj, objToStr, @default)
        {
            Section = section;
        }

        public ConfigWrapper(string key, string section, IConfigConverter<T> converter, T @default = default(T))
           : this(key, converter.ConvertFromString, converter.ConvertToString, @default)
        {
            Section = section;
        }

        protected virtual bool GetKeyExists()
        {
            return Config.HasEntry(Key, Section);
        }

        protected virtual T GetValue()
        {
            try
            {
                var strVal = Config.GetEntry(Key, _defaultStr, Section);
                var obj = _strToObj(strVal);

                // Always update in case config was changed from outside
                _lastValue = obj;
                _lastValueSet = true;

                return obj;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "ConfigWrapper Get Converter Exception: " + ex.Message);
                return _default;
            }
        }

        protected virtual void SetValue(T value)
        {
            try
            {
                // Always write just in case config was changed from outside
                var strVal = _objToStr(value);
                Config.SetEntry(Key, strVal, Section);

                if (_lastValueSet && Equals(_lastValue, value)) return;

                _lastValue = value;
                _lastValueSet = true;

                OnSettingChanged();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "ConfigWrapper Set Converter Exception: " + ex.Message);
            }
        }

        public void Clear()
        {
            Config.UnsetEntry(Key, Section);

            _lastValueSet = false;
            OnSettingChanged();
        }

        /// <summary>
        /// Fired when the setting is changed. Does not detect changes made outside from this object.
        /// </summary>
        public event EventHandler SettingChanged;

        private void OnSettingChanged()
        {
            SettingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
