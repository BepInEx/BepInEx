using System.Diagnostics;

namespace BepInEx.Logging;

/// <summary>
///     A source that routes all logs from the inbuilt .NET <see cref="Trace" /> API to the BepInEx logging system.
/// </summary>
/// <inheritdoc cref="TraceListener" />
public class TraceLogSource : TraceListener
{
    private static TraceLogSource traceListener;

    /// <summary>
    ///     Creates a new trace log source.
    /// </summary>
    protected TraceLogSource()
    {
        LogSource = new ManualLogSource("Trace");
    }

    /// <summary>
    ///     Whether Trace logs are currently being rerouted.
    /// </summary>
    public static bool IsListening { get; private set; }

    /// <summary>
    ///     Internal log source.
    /// </summary>
    protected ManualLogSource LogSource { get; }

    /// <summary>
    ///     Creates a new trace log source.
    /// </summary>
    /// <returns>New log source (or already existing one).</returns>
    public static ILogSource CreateSource()
    {
        if (traceListener == null)
        {
            traceListener = new TraceLogSource();
            Trace.Listeners.Add(traceListener);
            IsListening = true;
        }

        return traceListener.LogSource;
    }

    /// <summary>
    ///     Writes a message to the underlying <see cref="ManualLogSource" /> instance.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public override void Write(string message) => LogSource.Log(LogLevel.Info, message);

    /// <summary>
    ///     Writes a message and a newline to the underlying <see cref="ManualLogSource" /> instance.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public override void WriteLine(string message) => LogSource.Log(LogLevel.Info, message);

    /// <inheritdoc />
    public override void TraceEvent(TraceEventCache eventCache,
                                    string source,
                                    TraceEventType eventType,
                                    int id,
                                    string format,
                                    params object[] args) =>
        TraceEvent(eventCache, source, eventType, id, string.Format(format, args));

    /// <inheritdoc />
    public override void TraceEvent(TraceEventCache eventCache,
                                    string source,
                                    TraceEventType eventType,
                                    int id,
                                    string message)
    {
        var level = eventType switch
        {
            TraceEventType.Critical    => LogLevel.Fatal,
            TraceEventType.Error       => LogLevel.Error,
            TraceEventType.Warning     => LogLevel.Warning,
            TraceEventType.Information => LogLevel.Info,
            _                          => LogLevel.Debug
        };
        LogSource.Log(level, $"{message}".Trim());
    }
}
