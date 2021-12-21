using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BepInEx;

/// <summary>
///     Contains unofficial extensions to the underlying Mono runtime.
/// </summary>
public static class MonoExtensions
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern IntPtr GetFunctionPointerForDelegateInternal2(Delegate d, CallingConvention conv);

    public static IntPtr GetFunctionPointerForDelegate(Delegate d, CallingConvention conv)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));

        return GetFunctionPointerForDelegateInternal2(d, conv);
    }
}
