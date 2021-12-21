using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BepInEx.Logging;

/// <summary>
///     Logs entries using Unity specific outputs.
/// </summary>
public class DiskLogListener : ILogListener
{
    public static HashSet<string> BlacklistedSources = new();

    /// <summary>
    ///     Creates a new disk log listener.
    /// </summary>
    /// <param name="localPath">Path to the log.</param>
    /// <param name="displayedLogLevel">Log levels to display.</param>
    /// <param name="appendLog">Whether to append logs to an already existing log file.</param>
    /// <param name="delayedFlushing">
    ///     Whether to delay flushing to disk to improve performance. Useful to set this to false
    ///     when debugging crashes.
    /// </param>
    /// <param name="fileLimit">Maximum amount of concurrently opened log files. Can help with infinite game boot loops.</param>
    public DiskLogListener(string localPath,
                           LogLevel displayedLogLevel = LogLevel.Info,
                           bool appendLog = false,
                           bool delayedFlushing = true,
                           int fileLimit = 5)
    {
        DisplayedLogLevel = displayedLogLevel;

        var counter = 1;

        FileStream fileStream;

        while (!Utility.TryOpenFileStream(Path.Combine(Paths.BepInExRootPath, localPath),
                                          appendLog ? FileMode.Append : FileMode.Create, out fileStream,
                                          share: FileShare.Read, access: FileAccess.Write))
        {
            if (counter == fileLimit)
            {
                Logger.Log(LogLevel.Error, "Couldn't open a log file for writing. Skipping log file creation");

                return;
            }

            Logger.Log(LogLevel.Warning, $"Couldn't open log file '{localPath}' for writing, trying another...");

            localPath = $"LogOutput.{counter++}.log";
        }

        LogWriter = TextWriter.Synchronized(new StreamWriter(fileStream, Utility.UTF8NoBom));

        if (delayedFlushing) FlushTimer = new Timer(o => { LogWriter?.Flush(); }, null, 2000, 2000);

        InstantFlushing = !delayedFlushing;
    }

    /// <summary>
    ///     Log levels to display.
    /// </summary>
    public LogLevel DisplayedLogLevel { get; }

    /// <summary>
    ///     Writer for the disk log.
    /// </summary>
    public TextWriter LogWriter { get; protected set; }

    /// <summary>
    ///     Timer for flushing the logs to a file.
    /// </summary>
    private Timer FlushTimer { get; }

    private bool InstantFlushing { get; }

    /// <inheritdoc />
    public LogLevel LogLevelFilter => DisplayedLogLevel;

    /// <inheritdoc />
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if (LogWriter == null)
            return;

        if (BlacklistedSources.Contains(eventArgs.Source.SourceName))
            return;

        LogWriter.WriteLine(eventArgs.ToString());

        if (InstantFlushing)
            LogWriter.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        FlushTimer?.Dispose();

        try
        {
            LogWriter?.Flush();
            LogWriter?.Dispose();
        }
        catch (ObjectDisposedException) { }
    }

    ~DiskLogListener()
    {
        Dispose();
    }
}
