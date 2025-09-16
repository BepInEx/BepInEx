// --------------------------------------------------
// UnityInjector - ConsoleWindow.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using BepInEx;
using BepInEx.ConsoleUtil;
using static BepInEx.Core.PlatformUtils;

namespace UnityInjector.ConsoleUtil;

internal class ConsoleWindow
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int OBJID_WINDOW = 0;
    public static IntPtr ConsoleOutHandle;
    public static IntPtr OriginalStdoutHandle;

    private static bool methodsInited;
    private static SetForegroundWindowDelegate setForeground;
    private static GetForegroundWindowDelegate getForeground;
    private static GetSystemMenuDelegate getSystemMenu;
    private static DeleteMenuDelegate deleteMenu;
    private static SetWinEventHookDelegate setWinEventHook;
    private static UnhookWinEventDelegate unhookWinEvent;
    private static GetCurrentProcessIdDelegate getCurrentProcessId;

    private static IntPtr winEventHook;
    private static IntPtr consoleWindowHandle;
    private static ManualResetEventSlim consoleWindowReady;
    private static WinEventProc winEventProc;

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

    [SupportedOSPlatform("windows6.1")]
    public static Icon Icon
    {
        set
        {
            if (!IsAttached)
                return;

            if (value == null || value.Handle == IntPtr.Zero)
                throw new ArgumentNullException(nameof(value), "Icon handle is null or invalid");

            IntPtr consoleWindow = GetConsoleWindow();

            if (consoleWindow == IntPtr.Zero)
            {
                using (consoleWindowReady = new ManualResetEventSlim(false))
                {
                    winEventProc = WinEventCallback;
                    var processId = getCurrentProcessId();
                    winEventHook = setWinEventHook(
                        EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE,
                        IntPtr.Zero, winEventProc,
                        processId, 0, WINEVENT_OUTOFCONTEXT);

                    if (winEventHook == IntPtr.Zero)
                        throw new InvalidOperationException("Failed to set WinEvent hook");

                    try
                    {
                        consoleWindowReady.Wait();
                        consoleWindow = consoleWindowHandle;
                    }
                    finally
                    {
                        unhookWinEvent(winEventHook);
                        winEventHook = IntPtr.Zero;
                    }
                }
            }

            SendMessage(consoleWindow, WM_SETICON, ICON_SMALL, value.Handle);
            SendMessage(consoleWindow, WM_SETICON, ICON_BIG, value.Handle);
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
        setForeground = GetProcAddress(user32Dll, "SetForegroundWindow").AsDelegate<SetForegroundWindowDelegate>();
        getForeground = GetProcAddress(user32Dll, "GetForegroundWindow").AsDelegate<GetForegroundWindowDelegate>();
        getSystemMenu = GetProcAddress(user32Dll, "GetSystemMenu").AsDelegate<GetSystemMenuDelegate>();
        deleteMenu = GetProcAddress(user32Dll, "DeleteMenu").AsDelegate<DeleteMenuDelegate>();
        setWinEventHook = GetProcAddress(user32Dll, "SetWinEventHook").AsDelegate<SetWinEventHookDelegate>();
        unhookWinEvent = GetProcAddress(user32Dll, "UnhookWinEvent").AsDelegate<UnhookWinEventDelegate>();

        var kernel32Dll = LoadLibraryEx("kernel32.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
        getCurrentProcessId = GetProcAddress(kernel32Dll, "GetCurrentProcessId").AsDelegate<GetCurrentProcessIdDelegate>();
    }

    private static void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (eventType == EVENT_OBJECT_CREATE && idObject == OBJID_WINDOW)
        {
            var consoleWnd = GetConsoleWindow();
            if (consoleWnd != IntPtr.Zero && consoleWnd == hwnd)
            {
                consoleWindowHandle = hwnd;
                consoleWindowReady?.Set();
            }
        }
    }

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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();


    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetForegroundWindowDelegate(IntPtr hWnd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetForegroundWindowDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetSystemMenuDelegate(IntPtr hwnd, bool bRevert);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool DeleteMenuDelegate(IntPtr hMenu, uint uPosition, uint uFlags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SetWinEventHookDelegate(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool UnhookWinEventDelegate(IntPtr hWinEventHook);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint GetCurrentProcessIdDelegate();
}
