using System;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.IL2CPP.Hook;

public class UnhollowerDetourHandler : IManagedDetour
{
    public T Detour<T>(IntPtr from, T to) where T : Delegate
    {
        FunchookDetour.CreateAndApply(from, to, out var original);
        return original;
    }
}
