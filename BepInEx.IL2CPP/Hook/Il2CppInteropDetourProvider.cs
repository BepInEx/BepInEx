using Il2CppInterop.Runtime.Injection;

namespace BepInEx.IL2CPP.Hook;

internal class Il2CppInteropDetourProvider : IDetourProvider
{
    public IDetour Create(nint original, nint target) =>
        new Il2CppInteropDetour(INativeDetour.Create(original, target));
}

internal class Il2CppInteropDetour : IDetour
{
    private readonly INativeDetour detour;

    public Il2CppInteropDetour(INativeDetour detour)
    {
        this.detour = detour;
    }

    public void Dispose() => detour.Dispose();

    public void Apply() => detour.Apply();

    public nint Target => detour.OriginalMethodPtr;
    public nint Detour => detour.DetourMethodPtr;
    public nint OriginalTrampoline => detour.TrampolinePtr;
}
