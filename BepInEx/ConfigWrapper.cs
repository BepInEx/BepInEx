using System.ComponentModel;

namespace BepInEx
{
    public class ConfigWrapper<T>
    {
        private TypeConverter _converter;

        public T Default { get; protected set; }

        public bool Exists
        {
            get { return GetKeyExists(); }
        }

        public string Key { get; protected set; }

        public string Section { get; protected set; }

        public T Value
        {
            get { return GetValue(); }
            set { SetValue(value); }
        }

        public ConfigWrapper(string key, T @default = default(T))
        {
            Default = @default;
            Key = key;
        }

        public ConfigWrapper(string key, BaseUnityPlugin plugin, T @default = default(T)) : this(key, @default)
        {
            Section = TypeLoader.GetMetadata(plugin).GUID;
        }

        public ConfigWrapper(string key, string section, T @default = default(T)) : this(key, @default)
        {
            Section = section;
        }

        protected virtual bool GetKeyExists()
        {
            return Config.HasEntry(Key, Section);
        }

        protected virtual T GetValue()
        {
            if (_converter == null)
                _converter = TypeDescriptor.GetConverter(typeof(T));

            if (!Exists)
                return Default;

            var strVal = Config.GetEntry(Key, null, Section);
            return (T)_converter.ConvertFrom(strVal);
        }

        protected virtual void SetValue(T value)
        {
            if (_converter == null)
                _converter = TypeDescriptor.GetConverter(typeof(T));

            var strVal = _converter.ConvertToString(value);
            Config.SetEntry(Key, strVal, Section);
        }

        public static void RegisterTypeConverter<TC>() where TC : TypeConverter
        {
            TypeDescriptor.AddAttributes(typeof(T), new TypeConverterAttribute(typeof(TC)));
        }

        public void Clear()
        {
            Config.UnsetEntry(Key, Section);
        }
    }
}
