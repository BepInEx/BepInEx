using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Injection;
using MonoMod.Core;
using MonoMod.RuntimeDetour;

namespace BepInEx.Unity.IL2CPP.Hook;

internal class Il2CppInteropDetourProvider : IDetourProvider
{
    public IDetour Create<TDelegate>(nint original, TDelegate target) where TDelegate : Delegate
    {
        var targetPtr = Marshal.GetFunctionPointerForDelegate(target);
        return new Il2CppInteropDetour(DetourContext.CurrentFactory!.CreateNativeDetour(original, targetPtr));
    }
}

internal class Il2CppInteropDetour : IDetour
{
    private readonly ICoreNativeDetour detour;

    public Il2CppInteropDetour(ICoreNativeDetour detour)
    {
        this.detour = detour;
    }

    public void Dispose() => detour.Dispose();

    public void Apply() => detour.Apply();
    
    public nint Target => detour.Source;
    public nint Detour => detour.Target;
    public nint OriginalTrampoline => detour.OrigEntrypoint;
}
