using System;
using System.Reflection;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;

namespace BepInEx.IL2CPP
{

    [AttributeUsage(AttributeTargets.Class)]
    public class Il2CppInterfaces : Attribute
    {

        public Type[] Interfaces { get; protected set; }

        public Il2CppInterfaces(params Type[] interfaces)
        {
            Interfaces = interfaces;
        }

    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
    public class DontAutoRegisterInIl2Cpp : Attribute { }

    internal class PluginClassInjector
    {

        public static void RegisterAssemblyInIl2Cpp(Assembly pluginAssembly)
        {
            if (pluginAssembly.GetCustomAttribute<DontAutoRegisterInIl2Cpp>() != null) return;

            foreach (var type in pluginAssembly.DefinedTypes)
                if (typeof(Il2CppObjectBase).IsAssignableFrom(type) && !ClassInjector.IsTypeRegisteredInIl2Cpp(type))
                    RegisterTypeInIl2Cpp(type);
        }

        public static bool RegisterTypeInIl2Cpp(Type type)
        {
            if(type.Assembly.GetCustomAttribute<DontAutoRegisterInIl2Cpp>() != null) return false;
            if (type.GetCustomAttribute<DontAutoRegisterInIl2Cpp>() != null) return false;

            if (!ClassInjector.IsTypeRegisteredInIl2Cpp(type.BaseType))
                if (!RegisterTypeInIl2Cpp(type.BaseType))
                    return false;

            var interfaces = type.GetCustomAttribute<Il2CppInterfaces>();

            if (interfaces != null)
                ClassInjector.RegisterTypeInIl2CppWithInterfaces(type, true, interfaces.Interfaces);
            else
                ClassInjector.RegisterTypeInIl2Cpp(type);

            return true;
        }

    }
}
