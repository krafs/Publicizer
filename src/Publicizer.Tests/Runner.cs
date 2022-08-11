using System.Diagnostics;

namespace Publicizer.Tests
{
    internal static class Runner
    {
        internal static Process Run(string command, params string[] arguments)
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
            Process process = Process.Start(startInfo)!;
            process.WaitForExit();

            return process;
        }
    }
}
