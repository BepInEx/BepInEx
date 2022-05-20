using BepInEx.Configuration;
using BepInEx.IL2CPP.Hook.Dobby;
using BepInEx.IL2CPP.Hook.Funchook;
using System;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook;

public class NativeDetourHelper
{
    internal enum DetourProvider
    {
        Default,
        Dobby,
        Funchook
    }
    private static readonly ConfigEntry<DetourProvider> DetourProviderType = ConfigFile.CoreConfig.Bind(
        "Detours", "DetourProviderType",
        DetourProvider.Default,
        "The native provider to use for managed detours"
    );

    public static bool DEBUG_DETOURS = false;

    private static INativeDetour CreateDefault(nint original, nint target)
    {
        // TODO: check and provide an OS accurate provider
        return new DobbyDetour(original, target);
    }

    public static INativeDetour Create(nint original, nint target)
    {
        return DetourProviderType.Value switch
        {
            DetourProvider.Dobby => new DobbyDetour(original, target),
            DetourProvider.Funchook => new FunchookDetour(original, target),
            _ => CreateDefault(original, target)
        };
    }

    public static INativeDetour CreateAndApply<T>(nint from, T to, out T original, CallingConvention? callingConvention = null)
        where T : Delegate
    {
        var toPtr = callingConvention != null
            ? MonoExtensions.GetFunctionPointerForDelegate(to, callingConvention.Value)
            : Marshal.GetFunctionPointerForDelegate(to);

        var detour = Create(from, toPtr);
        original = detour.GenerateTrampoline<T>();
        detour.Apply();
        return detour;
    }
}
