using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;

namespace BepInEx.Unity.IL2CPP.Logging;

/// <summary>
///     Logs entries using IL2CPP specific outputs.
/// </summary>
public class IL2CPPLogSource : ILogSource
{
    /// <summary>
    ///     Creates an <see cref="IL2CPPLogSource"/>
    /// </summary>
    public IL2CPPLogSource()
    {
        var loggerPointer = Marshal.GetFunctionPointerForDelegate(new IL2CPPLogCallbackDelegate(IL2CPPLogCallback));
        Il2CppInterop.Runtime.IL2CPP.il2cpp_register_log_callback(loggerPointer);
    }

    /// <inheritdoc />
    public string SourceName { get; } = "IL2CPP";
    
    /// <inheritdoc />
    public event EventHandler<LogEventArgs> LogEvent;

    /// <inheritdoc />
    public void Dispose() { }

    private void IL2CPPLogCallback(string message) =>
        LogEvent?.Invoke(this, new LogEventArgs(message.Trim(), LogLevel.Message, this));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void IL2CPPLogCallbackDelegate([In] [MarshalAs(UnmanagedType.LPStr)] string message);
}
