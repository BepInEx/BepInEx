using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.ConsoleUtil;
using HarmonyLib;
using Microsoft.Win32.SafeHandles;
using MonoMod.Utils;
using UnityInjector.ConsoleUtil;

namespace BepInEx;

internal class WindowsConsoleDriver : IConsoleDriver
{
    // Apparently on some versions of Unity (e.g. 2018.4) using old mono causes crashes on game close if
    // IntPtr overload is used for file streams (check #139).
    // On the other hand, not all Unity games come with SafeFileHandle overload for FileStream
    // As such, we're trying to use SafeFileHandle when it's available and go back to IntPtr overload if not available
    private static readonly ConstructorInfo FileStreamCtor = new[]
    {
        AccessTools.Constructor(typeof(FileStream), new[] { typeof(SafeFileHandle), typeof(FileAccess) }),
        AccessTools.Constructor(typeof(FileStream), new[] { typeof(IntPtr), typeof(FileAccess) })
    }.FirstOrDefault(m => m != null);

    private readonly Func<int> getWindowHeight = AccessTools
                                                 .PropertyGetter(typeof(Console), nameof(Console.WindowHeight))
                                                 ?.CreateDelegate<Func<int>>();

    private readonly Func<int> getWindowWidth = AccessTools
                                                .PropertyGetter(typeof(Console), nameof(Console.WindowWidth))
                                                ?.CreateDelegate<Func<int>>();

    private int ConsoleWidth => getWindowWidth?.Invoke() ?? 0;
    private int ConsoleHeight => getWindowHeight?.Invoke() ?? 0;

    private bool useWinApiEncoder;

    public TextWriter StandardOut { get; private set; }
    public TextWriter ConsoleOut { get; private set; }

    public bool ConsoleActive { get; private set; }
    public bool ConsoleIsExternal => true;

    private bool TryCheckConsoleExists()
    {
        try
        {
            return ConsoleWidth != 0 && ConsoleHeight != 0;
        }
        catch (IOException)
        {
            //System.IO.IOException: The handle is invalid.
            //    at System.ConsolePal.GetBufferInfo(Boolean throwOnNoConsole, Boolean & succeeded)
            //at System.ConsolePal.get_WindowWidth()
            //at System.Console.get_WindowWidth()
            //at BepInEx.WindowsConsoleDriver.get_ConsoleWidth()
            //at BepInEx.WindowsConsoleDriver.Initialize(Boolean alreadyActive)

            return false;
        }
    }

    public void Initialize(bool alreadyActive, bool useWinApiEncoder)
    {
        ConsoleActive = alreadyActive || TryCheckConsoleExists();
        this.useWinApiEncoder = useWinApiEncoder;

        if (ConsoleActive)
        {
            // We're in a .NET framework / XNA environment; console *is* stdout
            ConsoleOut = Console.Out;
            StandardOut = new StreamWriter(Console.OpenStandardOutput());
        }
        else
        {
            StandardOut = Console.Out;
        }
    }

    public void CreateConsole(uint codepage)
    {
        ConsoleWindow.Attach();

        // Make sure of ConsoleEncoding helper class because on some Monos
        // Encoding.GetEncoding throws NotImplementedException on most codepages
        // NOTE: We don't set Console.OutputEncoding because it resets any existing Console.Out writers
        ConsoleEncoding.ConsoleCodePage = codepage;

        // If stdout exists, write to it, otherwise make it the same as console out
        // Not sure if this is needed? Does the original Console.Out still work?
        var stdout = GetOutHandle();
        if (stdout == IntPtr.Zero)
        {
            StandardOut = TextWriter.Null;
            ConsoleOut = TextWriter.Null;
            return;
        }

        var originalOutStream = OpenFileStream(stdout);
        StandardOut = new StreamWriter(originalOutStream, Utility.UTF8NoBom)
        {
            AutoFlush = true
        };

        var consoleOutStream = OpenFileStream(ConsoleWindow.ConsoleOutHandle);
        // Can't use Console.OutputEncoding because it can be null (i.e. not preference by user)
        ConsoleOut = new StreamWriter(consoleOutStream, useWinApiEncoder ? ConsoleEncoding.OutputEncoding : Utility.UTF8NoBom)
        {
            AutoFlush = true
        };
        ConsoleActive = true;
    }

    public void PreventClose() => ConsoleWindow.PreventClose();

    public void DetachConsole()
    {
        ConsoleWindow.Detach();

        ConsoleOut.Close();
        ConsoleOut = null;

        ConsoleActive = false;
    }

    public void SetConsoleColor(ConsoleColor color)
    {
        SafeConsole.ForegroundColor = color;
        Kon.ForegroundColor = color;
    }

    public void SetConsoleTitle(string title) => ConsoleWindow.Title = title;

    private static FileStream OpenFileStream(IntPtr handle)
    {
        var fileHandle = new SafeFileHandle(handle, false);
        var ctorParams = AccessTools.ActualParameters(FileStreamCtor,
                                                      new object[]
                                                      {
                                                          fileHandle, fileHandle.DangerousGetHandle(),
                                                          FileAccess.Write
                                                      });
        return (FileStream) Activator.CreateInstance(typeof(FileStream), ctorParams);
    }

    private IntPtr GetOutHandle()
    {
        switch (ConsoleManager.ConfigConsoleOutRedirectType.Value)
        {
            case ConsoleManager.ConsoleOutRedirectType.ConsoleOut:
                return ConsoleWindow.ConsoleOutHandle;
            case ConsoleManager.ConsoleOutRedirectType.StandardOut:
                return ConsoleWindow.OriginalStdoutHandle;
            case ConsoleManager.ConsoleOutRedirectType.Auto:
            default:
                return ConsoleWindow.OriginalStdoutHandle != IntPtr.Zero
                           ? ConsoleWindow.OriginalStdoutHandle
                           : ConsoleWindow.ConsoleOutHandle;
        }
    }
}
