using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;

/// <summary>
/// Simple logger implementation logging both to MSBuilds Build engine and an arbitrary Stream.
/// </summary>
internal sealed class Logger : ITaskLogger, IDisposable
{
    private readonly StreamWriter logFileWriter = StreamWriter.Null;
    private readonly TaskLoggingHelper taskLogger;
    private readonly string scope;

    private static string Now => DateTime.Now.ToLongTimeString();

    /// <summary>
    /// Constructs an instance of <see cref="Logger"/> that writes to both a Task and a Stream
    /// </summary>
    /// <param name="taskLogger">The logging helper of a Task</param>
    /// <param name="stream">An arbitrary stream for writing logs to</param>
    internal Logger(TaskLoggingHelper taskLogger, Stream stream)
    {
        logFileWriter = new StreamWriter(stream)
        {
            AutoFlush = true
        };
        this.taskLogger = taskLogger;
        scope = string.Empty;
    }

    /// <summary>
    /// Constructs an instance of <see cref="Logger"/> with a scope
    /// </summary>
    /// <param name="parentLogger"></param>
    /// <param name="scope">A string representing the scope of the logger. This will be written to each log entry in the log file</param>
    private Logger(Logger parentLogger, string scope)
    {
        logFileWriter = parentLogger.logFileWriter;
        taskLogger = parentLogger.taskLogger;
        this.scope = $" [{scope}]";
    }

    public void Error(string message)
    {
        taskLogger.LogError(message);
        Write("ERR", message);
    }

    public void Warning(string message)
    {
        taskLogger.LogWarning(message);
        Write("WRN", message);
    }

    public void Info(string message)
    {
        taskLogger.LogMessage(MessageImportance.Normal, message);
        Write("INF", message);
    }

    public void Verbose(string message)
    {
        taskLogger.LogMessage(MessageImportance.Low, message);
        Write("VRB", message);
    }

    private void Write(string logLevel, string message) => logFileWriter.WriteLine($"[{Now} {logLevel}]{scope} {message}");

    internal ITaskLogger CreateScope(string assemblyName) => new Logger(this, assemblyName);

    public void Dispose() => logFileWriter.Dispose();
}
