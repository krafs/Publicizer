namespace Publicizer.Tests;

/// <summary>Captures logged errors and warnings so parser diagnostics can be asserted on.</summary>
internal sealed class RecordingTaskLogger : ITaskLogger
{
    internal List<string> Errors { get; } = [];
    internal List<string> Warnings { get; } = [];

    public void Error(string message) => Errors.Add(message);
    public void Warning(string message) => Warnings.Add(message);
    public void Info(string message) { }
    public void Verbose(string message) { }
}
