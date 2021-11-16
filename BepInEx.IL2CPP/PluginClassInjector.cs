using System;
using System.Reflection;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;

namespace BepInEx.IL2CPP
{

    public class Il2CppInterfaces : Attribute
    {

        public Type[] Interfaces { get; protected set; }

        public Il2CppInterfaces(params Type[] interfaces)
        {
            Interfaces = interfaces;
        }

    }

    internal class PluginClassInjector
    {

        public static void RegisterAssemblyInIl2Cpp(Assembly pluginAssembly)
        {
            foreach (var type in pluginAssembly.DefinedTypes)
                if (typeof(Il2CppObjectBase).IsAssignableFrom(type) && !ClassInjector.IsTypeRegisteredInIl2Cpp(type))
                    RegisterTypeInIl2Cpp(type);
        }

        public static void RegisterTypeInIl2Cpp(Type type)
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp(type.BaseType))
                RegisterTypeInIl2Cpp(type.BaseType);

            var interfaces = type.GetCustomAttribute<Il2CppInterfaces>();

            if (interfaces != null)
                ClassInjector.RegisterTypeInIl2CppWithInterfaces(type, true, interfaces.Interfaces);
            else
                ClassInjector.RegisterTypeInIl2Cpp(type);
        }

    }
}
