namespace Publicizer.Tests;

/// <summary>Captures logged errors so parser diagnostics can be asserted on.</summary>
internal sealed class RecordingTaskLogger : ITaskLogger
{
    internal List<string> Errors { get; } = [];

    public void Error(string message) => Errors.Add(message);
    public void Warning(string message) { }
    public void Info(string message) { }
    public void Verbose(string message) { }
}
