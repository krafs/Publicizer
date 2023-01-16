using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;

/// <summary>
/// Simple logger implementation logging both to MSBuilds Build engine and an arbitrary Stream.
/// </summary>
internal sealed class Logger : ITaskLogger, IDisposable
{
    private readonly StreamWriter _logFileWriter = StreamWriter.Null;
    private readonly TaskLoggingHelper _taskLogger;
    private readonly string _scope;

    private static string Now => DateTime.Now.ToLongTimeString();

    /// <summary>
    /// Constructs an instance of <see cref="Logger"/> that writes to both a Task and a Stream
    /// </summary>
    /// <param name="taskLogger">The logging helper of a Task</param>
    /// <param name="stream">An arbitrary stream for writing logs to</param>
    internal Logger(TaskLoggingHelper taskLogger, Stream stream)
    {
        _logFileWriter = new StreamWriter(stream)
        {
            AutoFlush = true
        };
        _taskLogger = taskLogger;
        _scope = string.Empty;
    }

    /// <summary>
    /// Constructs an instance of <see cref="Logger"/> with a scope
    /// </summary>
    /// <param name="parentLogger"></param>
    /// <param name="scope">A string representing the scope of the logger. This will be written to each log entry in the log file</param>
    private Logger(Logger parentLogger, string scope)
    {
        _logFileWriter = parentLogger._logFileWriter;
        _taskLogger = parentLogger._taskLogger;
        _scope = $" [{scope}]";
    }

    public void Error(string message)
    {
        _taskLogger.LogError(message);
        Write("ERR", message);
    }

    public void Warning(string message)
    {
        _taskLogger.LogWarning(message);
        Write("WRN", message);
    }

    public void Info(string message)
    {
        _taskLogger.LogMessage(MessageImportance.Normal, message);
        Write("INF", message);
    }

    public void Verbose(string message)
    {
        _taskLogger.LogMessage(MessageImportance.Low, message);
        Write("VRB", message);
    }

    private void Write(string logLevel, string message)
    {
        _logFileWriter.WriteLine($"[{Now} {logLevel}]{_scope} {message}");
    }

    internal ITaskLogger CreateScope(string assemblyName)
    {
        return new Logger(this, assemblyName);
    }

    public void Dispose()
    {
        _logFileWriter.Dispose();
    }
}
