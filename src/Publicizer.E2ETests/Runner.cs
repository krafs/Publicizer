using System;
using System.Diagnostics;

namespace Publicizer.E2ETests;

internal static class Runner
{
    // Which MSBuild host builds the consumer projects. "dotnet" = Core MSBuild (.NET);
    // "msbuild" = desktop MSBuild.exe (.NET Framework), the host Visual Studio uses.
    // Selected by CI per matrix leg; defaults to dotnet so the suite runs anywhere.
    internal static string Builder => Environment.GetEnvironmentVariable("PUBLICIZER_TEST_BUILDER") ?? "dotnet";

    internal static ProcessResult Build(string projectPath)
    {
        return Builder.ToLowerInvariant() switch
        {
            // -restore (not -t:restore,build): restore in a separate evaluation so the build
            // sees the NuGet-generated imports that pull in Publicizer's props/targets.
            "msbuild" => Run("msbuild", projectPath, "-restore"),
            _ => Run("dotnet", "build", projectPath),
        };
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
