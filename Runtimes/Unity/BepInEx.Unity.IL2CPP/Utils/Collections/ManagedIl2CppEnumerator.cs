using System;
using System.Collections;
using HarmonyLib;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

/// <summary>
///     A wrapped <see cref="IEnumerator"/> to the IL2CPP system
/// </summary>
public class ManagedIl2CppEnumerator : IEnumerator
{
    private static readonly Func<Il2CppSystem.Collections.IEnumerator, bool> moveNext = AccessTools
        .Method(typeof(Il2CppSystem.Collections.IEnumerator), "MoveNext")
        ?.CreateDelegate<Func<Il2CppSystem.Collections.IEnumerator, bool>>();

    private static readonly Action<Il2CppSystem.Collections.IEnumerator> reset = AccessTools
        .Method(typeof(Il2CppSystem.Collections.IEnumerator), "Reset")
        ?.CreateDelegate<Action<Il2CppSystem.Collections.IEnumerator>>();

    private readonly Il2CppSystem.Collections.IEnumerator enumerator;

    /// <summary>
    ///     Creates a <see cref="ManagedIl2CppEnumerator"/> from an <see cref="Il2CppSystem.Collections.IEnumerator"/>
    /// </summary>
    /// <param name="enumerator">The enumerator to wrap</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerator"/> is null</exception>
    public ManagedIl2CppEnumerator(Il2CppSystem.Collections.IEnumerator enumerator)
    {
        this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    /// <inheritdoc />
    public bool MoveNext() => moveNext?.Invoke(enumerator) ?? false;

    /// <inheritdoc />
    public void Reset() => reset?.Invoke(enumerator);

    /// <inheritdoc />
    public object Current => enumerator.Current;
}
