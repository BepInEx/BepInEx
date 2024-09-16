﻿// --------------------------------------------------
// UnityInjector - ConsoleWindow.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.ConsoleUtil;
using MonoMod.Utils;

namespace UnityInjector.ConsoleUtil;

internal class ConsoleWindow
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;

    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
    public static IntPtr ConsoleOutHandle;
    public static IntPtr OriginalStdoutHandle;

    private static bool methodsInited;
    private static SetForegroundWindowDelegate setForeground;
    private static GetForegroundWindowDelegate getForeground;
    private static GetSystemMenuDelegate getSystemMenu;
    private static DeleteMenuDelegate deleteMenu;
    public static bool IsAttached { get; private set; }

    public static string Title
    {
        set
        {
            if (!IsAttached)
                return;

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length > 24500)
                throw new InvalidOperationException("Console title too long");

            if (!SetConsoleTitle(value))
                throw new InvalidOperationException("Console title invalid");
        }
    }

    public static void Attach()
    {
        if (IsAttached)
            return;
        Initialize();

        if (OriginalStdoutHandle == IntPtr.Zero)
            OriginalStdoutHandle = GetStdHandle(STD_OUTPUT_HANDLE);

        var cur = GetConsoleWindow();

        if (cur == IntPtr.Zero)
        {
            // Store Current Window
            var currWnd = getForeground();

            if (!AllocConsole())
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 5) throw new Win32Exception("AllocConsole() failed");
            }

            // Restore Foreground
            setForeground(currWnd);
        }

        ConsoleOutHandle = CreateFile("CONOUT$", 0x80000000 | 0x40000000, 2, IntPtr.Zero, 3, 0, IntPtr.Zero);
        Kon.conOut = ConsoleOutHandle;

        if (!SetStdHandle(STD_OUTPUT_HANDLE, ConsoleOutHandle))
            throw new Win32Exception("SetStdHandle() failed");

        if (OriginalStdoutHandle != IntPtr.Zero && ConsoleManager.ConfigConsoleOutRedirectType.Value ==
            ConsoleManager.ConsoleOutRedirectType.ConsoleOut)
            CloseHandle(OriginalStdoutHandle);

        IsAttached = true;
    }

    public static void PreventClose()
    {
        if (!IsAttached)
            return;
        Initialize();

        var hwnd = GetConsoleWindow();
        var hmenu = getSystemMenu(hwnd, false);
        if (hmenu != IntPtr.Zero)
            deleteMenu(hmenu, SC_CLOSE, MF_BYCOMMAND);
    }

    public static void Detach()
    {
        if (!IsAttached)
            return;

        if (!CloseHandle(ConsoleOutHandle))
            throw new Win32Exception("CloseHandle() failed");

        ConsoleOutHandle = IntPtr.Zero;

        if (!FreeConsole())
            throw new Win32Exception("FreeConsole() failed");

        if (!SetStdHandle(STD_OUTPUT_HANDLE, OriginalStdoutHandle))
            throw new Win32Exception("SetStdHandle() failed");

        IsAttached = false;
    }

    private static void Initialize()
    {
        if (methodsInited)
            return;
        methodsInited = true;

        // Some games may ship user32.dll with some methods missing. As such, we load the DLL explicitly from system folder
        var user32Dll = LoadLibraryEx("user32.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
        setForeground = AsDelegate<SetForegroundWindowDelegate>(GetProcAddress(user32Dll, "SetForegroundWindow"));
        getForeground = AsDelegate<GetForegroundWindowDelegate>(GetProcAddress(user32Dll, "GetForegroundWindow"));
        getSystemMenu = AsDelegate<GetSystemMenuDelegate>(GetProcAddress(user32Dll, "GetSystemMenu"));
        deleteMenu = AsDelegate<DeleteMenuDelegate>(GetProcAddress(user32Dll, "DeleteMenu"));
    }
    
    private static T AsDelegate<T>(IntPtr s) where T : class => Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CreateFile(string fileName,
                                            uint desiredAccess,
                                            int shareMode,
                                            IntPtr securityAttributes,
                                            int creationDisposition,
                                            int flagsAndAttributes,
                                            IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hConsoleOutput);

    [DllImport("kernel32.dll", BestFitMapping = true, CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetConsoleTitle(string title);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);


    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetForegroundWindowDelegate(IntPtr hWnd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetForegroundWindowDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetSystemMenuDelegate(IntPtr hwnd, bool bRevert);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool DeleteMenuDelegate(IntPtr hMenu, uint uPosition, uint uFlags);
}
