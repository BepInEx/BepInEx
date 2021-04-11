// --------------------------------------------------
// UnityInjector - ConsoleWindow.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.ConsoleUtil;

namespace UnityInjector.ConsoleUtil
{
    internal class ConsoleWindow
    {
        public static IntPtr ConsoleOutHandle;
        public static IntPtr OriginalStdoutHandle;
        public static bool IsAttached { get; private set; }

        public static string Title
        {
            set
            {
                if (!IsAttached)
                    return;

                if (value == null) throw new ArgumentNullException(nameof(value));

                if (value.Length > 24500) throw new InvalidOperationException("Console title too long");

                if (!SetConsoleTitle(value)) throw new InvalidOperationException("Console title invalid");
            }
        }

        public static void PreventClose()
        {
            if (!IsAttached)
                return;

            var hwnd = GetConsoleWindow();
            var hmenu = GetSystemMenu(hwnd, false);
            if (hmenu != IntPtr.Zero)
                DeleteMenu(hmenu, SC_CLOSE, MF_BYCOMMAND);
        }

        public static void Attach()
        {
            if (IsAttached)
                return;

            if (OriginalStdoutHandle == IntPtr.Zero)
                OriginalStdoutHandle = GetStdHandle(-11);

            // Store Current Window
            var currWnd = GetForegroundWindow();

            //Check for existing console before allocating
            if (GetConsoleWindow() == IntPtr.Zero)
                if (!AllocConsole())
                    throw new Exception("AllocConsole() failed");

            // Restore Foreground
            SetForegroundWindow(currWnd);

            ConsoleOutHandle = CreateFile("CONOUT$", 0x80000000 | 0x40000000, 2, IntPtr.Zero, 3, 0, IntPtr.Zero);
            Kon.conOut = ConsoleOutHandle;

            if (!SetStdHandle(-11, ConsoleOutHandle))
                throw new Exception("SetStdHandle() failed");

            if (OriginalStdoutHandle != IntPtr.Zero && ConsoleManager.ConfigConsoleOutRedirectType.Value ==
                ConsoleManager.ConsoleOutRedirectType.ConsoleOut)
                CloseHandle(OriginalStdoutHandle);

            IsAttached = true;
        }

        public static void Detach()
        {
            if (!IsAttached)
                return;

            if (!CloseHandle(ConsoleOutHandle))
                throw new Exception("CloseHandle() failed");

            ConsoleOutHandle = IntPtr.Zero;

            if (!FreeConsole())
                throw new Exception("FreeConsole() failed");

            if (!SetStdHandle(-11, OriginalStdoutHandle))
                throw new Exception("SetStdHandle() failed");

            IsAttached = false;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hwnd, bool bRevert);

        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;

        [DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);
    }
}
