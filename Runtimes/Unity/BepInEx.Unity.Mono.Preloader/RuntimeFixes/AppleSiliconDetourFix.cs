using System;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;

namespace BepInEx.Unity.Mono.Preloader.RuntimeFixes;

/// <summary>
///     Lets MonoMod write detours when the game runs as a native arm64 process on Apple Silicon.
/// </summary>
/// <remarks>
///     Unity has the <c>allow-jit</c> entitlement but not <c>allow-unsigned-executable-memory</c>, so its
///     code sits in a MAP_JIT region: <c>mprotect</c> returns EACCES and <c>mach_vm_write</c> returns
///     KERN_INVALID_ADDRESS. Only <c>pthread_jit_write_protect_np</c> gets in, and it cannot be called from
///     managed code, because it drops execute permission for the calling thread and that thread is running
///     JIT-compiled code. The toggle and the write must share one native frame, which is what doorstop's
///     <c>doorstop_jit_memcpy</c> is for.
/// </remarks>
internal static class AppleSiliconDetourFix
{
    /// <summary>Why the fix could not install. Logged by the preloader once its logger exists.</summary>
    public static Exception Exception { get; private set; }

    public static bool Applies =>
        IntPtr.Size == 8 && PlatformHelper.Is(Platform.MacOS) && PlatformHelper.Is(Platform.ARM);

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

    /// <summary>Replaces only the memory and code-writing operations; encoding stays with the inner platform.</summary>
    private sealed class AppleSiliconNativePlatform : IDetourNativePlatform
    {
        private const string LibSystem = "/usr/lib/libSystem.dylib";

        /// <summary>RTLD_DEFAULT on macOS. Searches every image already loaded into the process.</summary>
        private static readonly IntPtr RtldDefault = new(-2);

        /// <summary>Mirrors DetourNativeARMPlatform.DetourSizes, which is private.</summary>
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

        // doorstop_jit_memcpy handles permissions and the icache around its own write. The inner platform's
        // FlushICache would execute shellcode out of a managed byte[], which this process also may not do.
        public void MakeWritable(IntPtr src, uint size) { }
        public void MakeReadWriteExecutable(IntPtr src, uint size) { }
        public void MakeExecutable(IntPtr src, uint size) { }
        public void FlushICache(IntPtr src, uint size) { }

        public void Apply(NativeDetourData detour)
        {
            // The AArch64 detour is position independent, so encode it into scratch and copy it over whole.
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
