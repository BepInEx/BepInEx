using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx.Unity.Logging;

/// <summary>
///     Logs entries using Unity specific outputs.
/// </summary>
public class UnityLogSource : ILogSource
{
    private bool disposed;

    /// <summary>
    ///     Creates a new Unity log source.
    /// </summary>
    public UnityLogSource()
    {
        InternalUnityLogMessage += UnityLogMessageHandler;
    }

    /// <inheritdoc />
    public string SourceName { get; } = "Unity Log";

    /// <inheritdoc />
    public event EventHandler<LogEventArgs> LogEvent;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!disposed)
        {
            InternalUnityLogMessage -= UnityLogMessageHandler;
            disposed = true;
        }
    }

    private void UnityLogMessageHandler(object sender, LogEventArgs eventArgs)
    {
        var newEventArgs = new LogEventArgs(eventArgs.Data, eventArgs.Level, this);
        LogEvent?.Invoke(this, newEventArgs);
    }

    #region Static Unity handler

    private static event EventHandler<LogEventArgs> InternalUnityLogMessage;

    static UnityLogSource()
    {
        var callback = new Application.LogCallback(OnUnityLogMessageReceived);

        var logEvent =
            typeof(Application).GetEvent("logMessageReceived", BindingFlags.Public | BindingFlags.Static);
        if (logEvent != null)
        {
            logEvent.AddEventHandler(null, callback);
            //UnsubscribeAction = () => logEvent.RemoveEventHandler(null, callback);
        }
        else
        {
            var registerLogCallback =
                typeof(Application).GetMethod("RegisterLogCallback", BindingFlags.Public | BindingFlags.Static);
            registerLogCallback.Invoke(null, new object[] { callback });
            //UnsubscribeAction = () => registerLogCallback.Invoke(null, new object[] { null });
        }
    }

    private static void OnUnityLogMessageReceived(string message, string stackTrace, LogType type)
    {
        LogLevel logLevel;

        switch (type)
        {
            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception:
                logLevel = LogLevel.Error;
                break;
            case LogType.Warning:
                logLevel = LogLevel.Warning;
                break;
            case LogType.Log:
            default:
                logLevel = LogLevel.Info;
                break;
        }

        if (type == LogType.Exception)
            message += $"\nStack trace:\n{stackTrace}";

        InternalUnityLogMessage?.Invoke(null, new LogEventArgs(message, logLevel, null));
    }

    #endregion
}
