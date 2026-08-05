namespace Publicizer.Tests;

/// <summary>No-op <see cref="ITaskLogger"/> for exercising the engine directly.</summary>
internal sealed class NullTaskLogger : ITaskLogger
{
    internal static NullTaskLogger Instance { get; } = new();

    public void Error(string code, string message) { }
    public void Warning(string code, string message) { }
    public void Info(string message) { }
    public void Verbose(string message) { }
}
