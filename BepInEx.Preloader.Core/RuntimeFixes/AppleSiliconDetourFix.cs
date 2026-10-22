using System;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;

namespace BepInEx.Preloader.RuntimeFixes;

/// <summary>
///     Lets MonoMod write detours when the game runs as a native arm64 process on Apple Silicon.
/// </summary>
/// <remarks>
///     Unity's code sits in a MAP_JIT region, which refuses <c>mprotect</c> with EACCES and
///     <c>mach_vm_write</c> with KERN_INVALID_ADDRESS. Only <c>pthread_jit_write_protect_np</c> gets in, and
///     managed code cannot call it: it drops execute permission for the calling thread, which is running
///     JIT-compiled code. Doorstop's <c>doorstop_jit_memcpy</c> keeps the toggle and the write in one frame.
/// </remarks>
public static class AppleSiliconDetourFix
{
    /// <summary>Why the fix could not install. Logged once the preloader has a logger.</summary>
    public static Exception Exception { get; private set; }

    /// <summary>Whether this process is 64-bit arm64 macOS.</summary>
    public static bool Applies =>
        IntPtr.Size == 8 && PlatformHelper.Is(Platform.MacOS) && PlatformHelper.Is(Platform.ARM);

    /// <summary>Installs the platform. Call before any <see cref="DetourHelper" /> read.</summary>
    public static void Apply()
    {
        if (!Applies)
            return;

        try
        {
            DetourHelper.Native = new AppleSiliconNativePlatform(new DetourNativeARMPlatform());
        }
        catch (Exception e)
        {
            Exception = e;
        }
    }

    /// <summary>Replaces the write operations only; the inner platform still encodes.</summary>
    private sealed class AppleSiliconNativePlatform : IDetourNativePlatform
    {
        private const string LibSystem = "/usr/lib/libSystem.dylib";

        /// <summary>RTLD_DEFAULT: every loaded image.</summary>
        private static readonly IntPtr RtldDefault = new(-2);

        /// <summary>Mirrors private DetourNativeARMPlatform.DetourSizes (MonoMod 22.7.31.1).</summary>
        private static readonly uint[] DetourSizes = { 4 + 4, 4 + 2 + 2 + 4, 4 + 4, 4 + 4 + 4, 4 + 4 + 8 };

        private readonly IDetourNativePlatform inner;
        private readonly JitMemCpy jitMemCpy;

        public AppleSiliconNativePlatform(IDetourNativePlatform inner)
        {
            this.inner = inner;

            var symbol = dlsym(RtldDefault, "doorstop_jit_memcpy");

            if (symbol == IntPtr.Zero)
                throw new NotSupportedException(
                    "doorstop_jit_memcpy is not exported by any loaded image. Detours cannot be written on " +
                    "arm64 macOS without it. Update UnityDoorstop, or run the game under Rosetta.");

            jitMemCpy = (JitMemCpy) Marshal.GetDelegateForFunctionPointer(symbol, typeof(JitMemCpy));
        }

        // doorstop_jit_memcpy handles permissions and icache.
        public void MakeWritable(IntPtr src, uint size) { }
        public void MakeReadWriteExecutable(IntPtr src, uint size) { }
        public void MakeExecutable(IntPtr src, uint size) { }
        public void FlushICache(IntPtr src, uint size) { }

        public void Apply(NativeDetourData detour)
        {
            // Position independent, so encode then copy whole.
            var scratch = new byte[detour.Size];
            var pin = GCHandle.Alloc(scratch, GCHandleType.Pinned);

            try
            {
                var staging = detour;
                staging.Method = pin.AddrOfPinnedObject();
                inner.Apply(staging);
                jitMemCpy(detour.Method, staging.Method, (UIntPtr) detour.Size);
            }
            finally
            {
                pin.Free();
            }
        }

        public void Copy(IntPtr src, IntPtr dst, byte type)
        {
            if (type >= DetourSizes.Length)
                throw new NotSupportedException($"Unknown detour type {type}");

            jitMemCpy(dst, src, (UIntPtr) DetourSizes[type]);
        }

        public NativeDetourData Create(IntPtr from, IntPtr to, byte? type) => inner.Create(from, to, type);
        public void Free(NativeDetourData detour) => inner.Free(detour);
        public IntPtr MemAlloc(uint size) => inner.MemAlloc(size);
        public void MemFree(IntPtr ptr) => inner.MemFree(ptr);

        [DllImport(LibSystem)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void JitMemCpy(IntPtr dst, IntPtr src, UIntPtr n);
    }
}
