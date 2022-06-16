using System;
using System.Collections.Generic;
using System.Reflection;
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

        if (!ReflectionHelper.IsMono)
        {
            return new CacheDetourWrapper<T>(detour, original, to);
        }

        return detour;
    }

    // Workaround for CoreCLR collecting all delegates
    private class CacheDetourWrapper<T> : INativeDetour where T : Delegate
    {
        private readonly INativeDetour _wrapped;

        private List<T> _cache = new();

        public CacheDetourWrapper(INativeDetour wrapped, T original, T to)
        {
            _wrapped = wrapped;
            _cache.Add(original);
            _cache.Add(to);
        }

        public void Dispose()
        {
            _wrapped.Dispose();
            _cache.Clear();
        }

        public void Apply() => _wrapped.Apply();

        public void Undo() => _wrapped.Undo();

        public void Free() => _wrapped.Free();

        public MethodBase GenerateTrampoline(MethodBase signature = null) => _wrapped.GenerateTrampoline(signature);

        public T GenerateTrampoline<T>() where T : Delegate => _wrapped.GenerateTrampoline<T>();

        public bool IsValid => _wrapped.IsValid;

        public bool IsApplied => _wrapped.IsApplied;

        public nint OriginalMethodPtr => _wrapped.OriginalMethodPtr;

        public nint DetourMethodPtr => _wrapped.DetourMethodPtr;

        public nint TrampolinePtr => _wrapped.TrampolinePtr;
    }

    internal enum DetourProvider
    {
        Default,
        Dobby,
        Funchook
    }
}
