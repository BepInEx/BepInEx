using System;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using BepInEx.IL2CPP.Hook.Dobby;
using BepInEx.IL2CPP.Hook.Funchook;
using MonoMod.RuntimeDetour;

namespace BepInEx.IL2CPP.Hook;

public interface INativeDetour : IDetour
{
    private static readonly ConfigEntry<DetourProvider> DetourProviderType = ConfigFile.CoreConfig.Bind(
         "Detours", "DetourProviderType",
         DetourProvider.Default,
         "The native provider to use for managed detours"
        );

    public nint OriginalMethodPtr { get; }
    public nint DetourMethodPtr { get; }
    public nint TrampolinePtr { get; }

    private static INativeDetour CreateDefault(nint original, nint target) =>
        // TODO: check and provide an OS accurate provider
        new DobbyDetour(original, target);

    public static INativeDetour Create(nint original, nint target) =>
        DetourProviderType.Value switch
        {
            DetourProvider.Dobby    => new DobbyDetour(original, target),
            DetourProvider.Funchook => new FunchookDetour(original, target),
            _                       => CreateDefault(original, target)
        };

    public static INativeDetour CreateAndApply<T>(nint from, T to, out T original)
        where T : Delegate
    {
        var toPtr = Marshal.GetFunctionPointerForDelegate(to);
        var detour = Create(from, toPtr);
        original = detour.GenerateTrampoline<T>();
        detour.Apply();
        return detour;
    }

    internal enum DetourProvider
    {
        Default,
        Dobby,
        Funchook
    }
}
