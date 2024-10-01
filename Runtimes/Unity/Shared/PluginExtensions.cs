using System;
using UnityEngine;

#if UNITY_MONO
using BepInEx.Unity.Mono.Bootstrap;
#elif UNITY_IL2CPP
using BepInEx.Unity.IL2CPP.Utils;
#endif

namespace BepInEx.Unity.Shared;

/// <summary>
///     Extension class for plugins in Unity
/// </summary>
public static class PluginExtensions
{
    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    /// </summary>
    /// <param name="plugin">The plugin</param>
    /// <typeparam name="T">Type of the component to add.</typeparam>
    public static Component AddUnityComponent<T>(this Plugin plugin)
        where T : Component
    {
#if UNITY_MONO
        return UnityChainloader.ManagerObject.AddComponent(typeof(T));
#elif UNITY_IL2CPP
        return Il2CppUtils.AddComponent(typeof(T));
#endif
    }

    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    /// </summary>
    /// <param name="plugin">The plugin</param>
    /// <param name="type">Type of the component to add</param>
    public static Component AddUnityComponent(this Plugin plugin, Type type)
    {
#if UNITY_MONO
        return UnityChainloader.ManagerObject.AddComponent(type);
#elif UNITY_IL2CPP
        return Il2CppUtils.AddComponent(type);
#endif
    }
}
