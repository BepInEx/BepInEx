using System;
using UnhollowerRuntimeLib;

namespace BepInEx.IL2CPP.Hook;

public class UnhollowerDetourHandler : IManagedDetour
{
    public T Detour<T>(IntPtr from, T to) where T : Delegate
    {
        FastNativeDetour.CreateAndApply(from, to, out var original);
        return original;
    }
}
