using System;
using System.Diagnostics;

namespace Publicizer.E2ETests;

internal static class Runner
{
    // Which MSBuild host builds the consumer projects. "dotnet" = Core MSBuild (.NET);
    // "msbuild" = desktop MSBuild.exe (.NET Framework), the host Visual Studio uses.
    // Selected by CI per matrix leg; defaults to dotnet so the suite runs anywhere.
    internal static string Builder => Environment.GetEnvironmentVariable("PUBLICIZER_TEST_BUILDER") ?? "dotnet";

    internal static bool UsesDesktopMSBuild => Builder.Equals("msbuild", StringComparison.OrdinalIgnoreCase);

    // Extra arguments go to the host verbatim, so use only spellings both accept (-p:, -t:).
    internal static ProcessResult Build(string projectPath, params string[] extraArguments) => Build(projectPath, reuseNodes: false, extraArguments);

    // reuseNodes leaves the build node alive afterwards, holding the task assembly loaded —
    // what a Visual Studio session does between builds. Off by default so tests don't serve
    // each other stale nodes; on for the case that is about exactly that.
    internal static ProcessResult Build(string projectPath, bool reuseNodes, string[] extraArguments)
    {
        // -restore (not -t:restore,build): restore in a separate evaluation so the build
        // sees the NuGet-generated imports that pull in Publicizer's props/targets.
        string nodeReuse = $"-nodeReuse:{(reuseNodes ? "true" : "false")}";
        string[] arguments = UsesDesktopMSBuild
            ? ["-nologo", "-v:m", nodeReuse, projectPath, "-restore", .. extraArguments]
            : ["build", "-nologo", "-v:m", nodeReuse, projectPath, .. extraArguments];

        return Run(UsesDesktopMSBuild ? "msbuild" : "dotnet", arguments);
    }

    internal static ProcessResult Run(string command, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo)!;

        // Drain both streams concurrently before waiting: a chatty child (desktop
        // msbuild -restore) can fill a pipe buffer and block on write while we block on
        // WaitForExit, deadlocking. Reading first avoids that.
        System.Threading.Tasks.Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        System.Threading.Tasks.Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var result = new ProcessResult(
            ExitCode: process.ExitCode,
            Output: outputTask.Result,
            Error: errorTask.Result
        );

        process.Close();

        return result;
    }
}
