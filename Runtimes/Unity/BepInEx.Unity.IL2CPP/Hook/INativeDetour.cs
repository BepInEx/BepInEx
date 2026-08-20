using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Hook.Dobby;
using BepInEx.Unity.IL2CPP.Hook.Funchook;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace BepInEx.Unity.IL2CPP.Hook;

public interface INativeDetour : IDetour
{
    private static readonly ConfigEntry<DetourProvider> DetourProviderType = ConfigFile.CoreConfig.Bind(
         "Detours", "DetourProviderType",
         DetourProvider.Default,
         "The native provider to use for managed detours"
        );

    private static readonly ManualLogSource ThunkLog = Logger.CreateLogSource("NativeDetour");

    public nint OriginalMethodPtr { get; }
    public nint DetourMethodPtr { get; }
    public nint TrampolinePtr { get; }

    private static INativeDetour CreateDefault<T>(nint original, T target) where T : Delegate =>
        // TODO: check and provide an OS accurate provider
        new DobbyDetour(original, target);

    public static INativeDetour Create<T>(nint original, T target) where T : Delegate
    {
        var resolved = FollowExportThunks(original);
        if (resolved != original)
            ThunkLog.LogDebug($"Resolved export thunk 0x{original:X} -> 0x{resolved:X}");
        original = resolved;

        var detour = DetourProviderType.Value switch
        {
            DetourProvider.Dobby    => new DobbyDetour(original, target),
            DetourProvider.Funchook => new FunchookDetour(original, target),
            _                       => CreateDefault(original, target)
        };
        if (!ReflectionHelper.IsMono)
        {
            return new CacheDetourWrapper(detour, target);
        }

        return detour;
    }

    /// <summary>
    /// PE exports are often a <c>jmp rel32</c> (or <c>jmp [rip+disp]</c>) stub with int3 padding.
    /// Dobby aborts if asked to patch that stub. Follow to the real prologue first.
    /// No-op when the pointer is already a function body.
    /// </summary>
    private static nint FollowExportThunks(nint func)
    {
        if (func == 0) return func;
        var fn = func;
        for (var hops = 0; hops < 8; hops++)
        {
            byte op;
            try { op = Marshal.ReadByte(fn); }
            catch { return fn; }

            if (op == 0xE9)
            {
                fn += 5 + Marshal.ReadInt32(fn + 1);
                continue;
            }

            if (op == 0xFF && Marshal.ReadByte(fn + 1) == 0x25)
            {
                if (IntPtr.Size == 8)
                    fn = Marshal.ReadIntPtr(fn + 6 + Marshal.ReadInt32(fn + 2));
                else
                    fn = Marshal.ReadIntPtr((nint)(uint)Marshal.ReadInt32(fn + 2));
                continue;
            }

            break;
        }

        return fn;
    }

    public static INativeDetour CreateAndApply<T>(nint from, T to, out T original)
        where T : Delegate
    {
        var detour = Create(from, to);
        original = detour.GenerateTrampoline<T>();
        detour.Apply();

        return detour;
    }

    // Workaround for CoreCLR collecting all delegates
    private class CacheDetourWrapper : INativeDetour
    {
        private readonly INativeDetour _wrapped;

        private List<object> _cache = new();

        public CacheDetourWrapper(INativeDetour wrapped, Delegate target)
        {
            _wrapped = wrapped;
            _cache.Add(target);
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

        public T GenerateTrampoline<T>() where T : Delegate
        {
            var trampoline = _wrapped.GenerateTrampoline<T>();
            _cache.Add(trampoline);
            return trampoline;
        }

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
