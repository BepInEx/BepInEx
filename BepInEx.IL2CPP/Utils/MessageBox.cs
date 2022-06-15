using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Utils;

internal static class MessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int MessageBoxA(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    public static void Show(string text, string caption)
    {
        if (!PlatformHelper.Is(Platform.Windows)) throw new PlatformNotSupportedException();
        if (MessageBoxA(IntPtr.Zero, text, caption, 0) == 0)
            throw new Win32Exception();
    }
}
