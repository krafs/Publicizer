using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Publicizer.Tests;

/// <summary>
/// In-memory <see cref="IBuildEngine"/> so <see cref="PublicizeAssemblies"/> can be
/// driven directly in-process. Captures logged errors/warnings/messages for assertions.
/// </summary>
internal sealed class FakeBuildEngine : IBuildEngine
{
    internal List<string> Errors { get; } = [];
    internal List<string> Warnings { get; } = [];
    internal List<string> Messages { get; } = [];

    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e.Message ?? string.Empty);
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e.Message ?? string.Empty);
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e.Message ?? string.Empty);
    public void LogCustomEvent(CustomBuildEventArgs e) { }

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;
}
