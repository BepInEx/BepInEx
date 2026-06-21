using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Logging;

namespace BepInEx;

public static class InputConsole
{
    private const string PROMPT = "> ";

    private static FilteredConsoleLogListener _listener;
    private static Thread _inputThread;
    private static volatile bool _running;

    private static readonly StringBuilder _inputBuffer = new();
    private static int _cursorCharPos;
    private static readonly object _consoleLock = new();

    private static readonly System.Collections.Generic.List<string> _history = new();
    private static int _historyIndex = -1;
    private static string _savedInput;

    private static readonly System.Collections.Generic.Dictionary<string, string> _guidToSource =
        new(StringComparer.OrdinalIgnoreCase);

    private static TextWriter ConsoleWriter => ConsoleManager.ConsoleStream ?? Console.Out;

    public static void RegisterPluginSource(string guid, string sourceName)
    {
        lock (_consoleLock)
            _guidToSource[guid] = sourceName;
    }

    public static void Start(FilteredConsoleLogListener listener)
    {
        if (_running)
            return;

        _listener = listener;

        var toRemove = Logger.Listeners.Where(l => l is ConsoleLogListener).ToList();
        foreach (var oldListener in toRemove)
            Logger.Listeners.Remove(oldListener);

        _running = true;

        _inputThread = new Thread(InputLoop)
        {
            Name = "BepInEx_InputConsole",
            IsBackground = true
        };
        _inputThread.Start();

        lock (_consoleLock)
            RedrawInputLine();
    }

    public static void Stop()
    {
        _running = false;
        _inputThread = null;
        _listener = null;

        lock (_consoleLock)
        {
            _inputBuffer.Length = 0;
            _cursorCharPos = 0;
        }
    }

    public static void WriteLogLine(string logLine, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            bool hasInput = _inputBuffer.Length > 0;

            if (hasInput)
                EraseInputLine();

            ConsoleManager.SetConsoleColor(color);
            ConsoleWriter.Write(logLine);
            ConsoleWriter.Flush();
            ConsoleManager.SetConsoleColor(ConsoleColor.Gray);

            if (hasInput)
                RedrawInputLine();
        }
    }

    private static void InputLoop()
    {
        while (_running)
        {
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(true);
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(100);
                continue;
            }
            catch (Exception)
            {
                break;
            }

            lock (_consoleLock)
            {
                ProcessKey(key);
                EraseInputLine();
                RedrawInputLine();
            }
        }
    }

    private static void ProcessKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var input = _inputBuffer.ToString();
                _inputBuffer.Length = 0;
                _cursorCharPos = 0;
                _historyIndex = -1;
                _savedInput = null;
                EraseInputLine();
                ConsoleWriter.Flush();

                if (!string.IsNullOrEmpty(input.Trim()))
                {
                    if (_history.Count == 0 || _history[_history.Count - 1] != input)
                        _history.Add(input);
                    if (_history.Count > 50)
                        _history.RemoveAt(0);
                }

                if (input.StartsWith("/"))
                    ProcessCommand(input);
                break;

            case ConsoleKey.Backspace:
                if (_cursorCharPos > 0)
                {
                    if (_historyIndex != -1)
                        ExitHistoryMode();
                    _inputBuffer.Remove(_cursorCharPos - 1, 1);
                    _cursorCharPos--;
                }
                break;

            case ConsoleKey.Delete:
                if (_cursorCharPos < _inputBuffer.Length)
                {
                    if (_historyIndex != -1)
                        ExitHistoryMode();
                    _inputBuffer.Remove(_cursorCharPos, 1);
                }
                break;

            case ConsoleKey.UpArrow:
                if (_history.Count == 0)
                    break;
                if (_historyIndex == -1)
                {
                    _savedInput = _inputBuffer.ToString();
                    _historyIndex = _history.Count - 1;
                }
                else if (_historyIndex > 0)
                {
                    _historyIndex--;
                }
                _inputBuffer.Length = 0;
                _inputBuffer.Append(_history[_historyIndex]);
                _cursorCharPos = _inputBuffer.Length;
                break;

            case ConsoleKey.DownArrow:
                if (_historyIndex == -1)
                    break;
                if (_historyIndex < _history.Count - 1)
                {
                    _historyIndex++;
                    _inputBuffer.Length = 0;
                    _inputBuffer.Append(_history[_historyIndex]);
                }
                else
                {
                    _historyIndex = -1;
                    _inputBuffer.Length = 0;
                    _inputBuffer.Append(_savedInput ?? "");
                    _savedInput = null;
                }
                _cursorCharPos = _inputBuffer.Length;
                break;

            case ConsoleKey.LeftArrow:
                if (_cursorCharPos > 0)
                    _cursorCharPos--;
                break;

            case ConsoleKey.RightArrow:
                if (_cursorCharPos < _inputBuffer.Length)
                    _cursorCharPos++;
                break;

            case ConsoleKey.Home:
                _cursorCharPos = 0;
                break;

            case ConsoleKey.End:
                _cursorCharPos = _inputBuffer.Length;
                break;

            case ConsoleKey.Escape:
                _inputBuffer.Length = 0;
                _cursorCharPos = 0;
                break;

            default:
                if (key.KeyChar >= ' ')
                {
                    if (_historyIndex != -1)
                        ExitHistoryMode();
                    _inputBuffer.Insert(_cursorCharPos, key.KeyChar);
                    _cursorCharPos++;
                }
                break;
        }
    }

    private static bool SourceExists(string name)
    {
        foreach (var source in Logger.Sources)
        {
            if (string.Equals(source.SourceName, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void ExitHistoryMode()
    {
        _historyIndex = -1;
        _savedInput = null;
    }

    private static void EraseInputLine()
    {
        try
        {
            int top = Console.CursorTop;
            Console.SetCursorPosition(0, top);
            ConsoleWriter.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, top);
        }
        catch
        {
        }
    }

    private static void RedrawInputLine()
    {
        try
        {
            string display = PROMPT + _inputBuffer.ToString();
            ConsoleWriter.Write(display);

            if (_cursorCharPos < _inputBuffer.Length)
            {
                int cursorLeft = PROMPT.Length + _cursorCharPos;
                int cursorTop = Console.CursorTop;
                int width = Console.WindowWidth;
                if (width > 0)
                {
                    cursorTop -= (PROMPT.Length + _inputBuffer.Length) / width;
                    cursorLeft %= width;
                }

                Console.SetCursorPosition(cursorLeft, cursorTop);
            }
        }
        catch
        {
        }
    }

    private static void ProcessCommand(string input)
    {
        var parts = input.Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        switch (command)
        {
            case "/help":
                PrintHelp();
                break;

            case "/show":
                if (string.IsNullOrEmpty(arg))
                {
                    _listener.SourceFilter = null;
                    ConsoleWriter.WriteLine("[InputConsole] Showing all log sources");
                }
                else if (SourceExists(arg))
                {
                    _listener.SourceFilter = arg;
                    ConsoleWriter.WriteLine($"[InputConsole] Showing only: {arg}");
                }
                else if (_guidToSource.TryGetValue(arg, out var mappedName))
                {
                    _listener.SourceFilter = mappedName;
                    ConsoleWriter.WriteLine($"[InputConsole] Showing only: {mappedName} (plugin: {arg})");
                }
                else
                {
                    ConsoleWriter.WriteLine($"[InputConsole] Source not found: {arg}. Use /sources to list available sources.");
                }
                break;

            case "/sources":
                PrintSources();
                break;

            case "/clear":
                try { Console.Clear(); } catch { }
                break;

            default:
                ConsoleWriter.WriteLine($"[InputConsole] Unknown command: {command}. Type /help for available commands.");
                break;
        }
    }

    private static void PrintHelp()
    {
        ConsoleWriter.WriteLine("[InputConsole] Available commands:");
        ConsoleWriter.WriteLine("  /help              - Show this help");
        ConsoleWriter.WriteLine("  /show              - Show all log sources (reset filter)");
        ConsoleWriter.WriteLine("  /show <source>     - Show only logs from the specified source");
        ConsoleWriter.WriteLine("  /sources           - List all registered log sources");
        ConsoleWriter.WriteLine("  /clear             - Clear the console");
    }

    private static void PrintSources()
    {
        ConsoleWriter.WriteLine("[InputConsole] Registered log sources:");
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in Logger.Sources)
        {
            if (seen.Add(source.SourceName))
            {
                var guidNote = "";
                foreach (var kv in _guidToSource)
                {
                    if (string.Equals(kv.Value, source.SourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        guidNote = $"  (plugin: {kv.Key})";
                        break;
                    }
                }
                ConsoleWriter.WriteLine($"  {source.SourceName}{guidNote}");
            }
        }
    }
}
