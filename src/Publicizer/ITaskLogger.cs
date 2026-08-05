namespace Publicizer;

/// <remarks>
/// Internal: the package ships the task assembly under build/, never lib/, so nothing outside this
/// assembly can reference this type. Keeping it public would make its shape API by accident.
/// </remarks>
internal interface ITaskLogger
{
    /// <param name="code">A <see cref="DiagnosticCode"/> value, so the diagnostic can be suppressed and filtered.</param>
    /// <param name="message">The diagnostic text.</param>
    void Error(string code, string message);

    /// <param name="code">A <see cref="DiagnosticCode"/> value, so the diagnostic can be suppressed and filtered.</param>
    /// <param name="message">The diagnostic text.</param>
    void Warning(string code, string message);

    void Info(string message);
    void Verbose(string message);
}
