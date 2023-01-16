using System.Diagnostics;

namespace Publicizer.Tests;

internal static class Runner
{
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
        using Process process = Process.Start(startInfo)!;
        process.WaitForExit();

        var result = new ProcessResult(
            ExitCode: process.ExitCode,
            Output: process.StandardOutput.ReadToEnd(),
            Error: process.StandardError.ReadToEnd()
        );

        process.Close();

        return result;
    }
}
