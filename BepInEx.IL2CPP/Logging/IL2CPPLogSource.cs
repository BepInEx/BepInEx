using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;

namespace BepInEx.IL2CPP.Logging;

public class IL2CPPLogSource : ILogSource
{
    public IL2CPPLogSource()
    {
        var loggerPointer = Marshal.GetFunctionPointerForDelegate(new IL2CPPLogCallbackDelegate(IL2CPPLogCallback));
        UnhollowerBaseLib.IL2CPP.il2cpp_register_log_callback(loggerPointer);
    }

    public string SourceName { get; } = "IL2CPP";
    public event EventHandler<LogEventArgs> LogEvent;

    public void Dispose() { }

    private void IL2CPPLogCallback(string message) =>
        LogEvent?.Invoke(this, new LogEventArgs(message.Trim(), LogLevel.Message, this));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void IL2CPPLogCallbackDelegate([In] [MarshalAs(UnmanagedType.LPStr)] string message);
}
