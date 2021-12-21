using System;
using System.Collections;
using HarmonyLib;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Utils.Collections;

public class ManagedIl2CppEnumerator : IEnumerator
{
    private static readonly Func<Il2CppSystem.Collections.IEnumerator, bool> moveNext = AccessTools
        .Method(typeof(Il2CppSystem.Collections.IEnumerator), "MoveNext")
        ?.CreateDelegate<Func<Il2CppSystem.Collections.IEnumerator, bool>>();

    private static readonly Action<Il2CppSystem.Collections.IEnumerator> reset = AccessTools
        .Method(typeof(Il2CppSystem.Collections.IEnumerator), "Reset")
        ?.CreateDelegate<Action<Il2CppSystem.Collections.IEnumerator>>();

    private readonly Il2CppSystem.Collections.IEnumerator enumerator;

    public ManagedIl2CppEnumerator(Il2CppSystem.Collections.IEnumerator enumerator)
    {
        this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    public bool MoveNext() => moveNext?.Invoke(enumerator) ?? false;

    public void Reset() => reset?.Invoke(enumerator);

    public object Current => enumerator.Current;
}
