using System;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx.Unity.IL2CPP.Logging;

/// <summary>
///     Logs entries using Unity related IL2CPP specific outputs.
/// </summary>
public class IL2CPPUnityLogSource : ILogSource
{
    /// <summary>
    ///     Creates an <see cref="IL2CPPLogSource"/>
    /// </summary>
    public IL2CPPUnityLogSource()
    {
        Application.s_LogCallbackHandler = new Action<string, string, LogType>(UnityLogCallback);

        Il2CppInterop.Runtime.IL2CPP
                     .ResolveICall<
                         SetLogCallbackDefinedDelegate>("UnityEngine.Application::SetLogCallbackDefined")(true);
    }

    /// <inheritdoc />
    public string SourceName { get; } = "Unity";

    /// <inheritdoc />
    public event EventHandler<LogEventArgs> LogEvent;

    /// <inheritdoc />
    public void Dispose() { }

    private void UnityLogCallback(string logLine, string exception, LogType type)
    {
        var level = type switch
        {
            LogType.Error     => LogLevel.Error,
            LogType.Assert    => LogLevel.Debug,
            LogType.Warning   => LogLevel.Warning,
            LogType.Log       => LogLevel.Message,
            LogType.Exception => LogLevel.Error,
            _                 => LogLevel.Message
        };
        LogEvent?.Invoke(this, new LogEventArgs(logLine, level, this));
    }

    private delegate IntPtr SetLogCallbackDefinedDelegate(bool defined);
}
