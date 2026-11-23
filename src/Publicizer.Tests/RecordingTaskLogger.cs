namespace Publicizer.Tests;

/// <summary>
/// Captures logged errors and warnings so parser diagnostics can be asserted on. Entries are
/// recorded as <c>"PUB0001: message"</c>, so a test can assert on the code, the text, or both.
/// </summary>
internal sealed class RecordingTaskLogger : ITaskLogger
{
    internal List<string> Errors { get; } = [];
    internal List<string> Warnings { get; } = [];
    internal List<string> ErrorCodes { get; } = [];
    internal List<string> WarningCodes { get; } = [];

    public void Error(string code, string message)
    {
        Errors.Add($"{code}: {message}");
        ErrorCodes.Add(code);
    }

    public void Warning(string code, string message)
    {
        Warnings.Add($"{code}: {message}");
        WarningCodes.Add(code);
    }

    public void Info(string message) { }
    public void Verbose(string message) { }
}
