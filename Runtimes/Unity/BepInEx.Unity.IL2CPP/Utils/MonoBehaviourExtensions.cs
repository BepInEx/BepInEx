using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace BepInEx.Unity.IL2CPP.Utils;

/// <summary>
///     Extension classes on <see cref="MonoBehaviour"/>
/// </summary>
public static class MonoBehaviourExtensions
{
    /// <summary>
    ///     Starts a coroutine wrapped to IL2CPP
    /// </summary>
    /// <param name="self">The MonoBehavior</param>
    /// <param name="coroutine">The coroutine to execute</param>
    /// <returns>The coroutine reference</returns>
    public static Coroutine StartCoroutine(this MonoBehaviour self, IEnumerator coroutine) =>
        self.StartCoroutine(coroutine.WrapToIl2Cpp());
}
