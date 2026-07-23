using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Minimal hand-rolled approval testing. Compares a produced string against a
/// committed <c>*.verified.txt</c> under <c>Snapshots/</c> next to the test source.
/// On mismatch (or a missing approved file) it writes a <c>*.received.txt</c>
/// beside it and fails, so the diff is reviewable in the working tree.
///
/// To (re)generate approved files, run the tests with environment variable
/// <c>VERIFY_APPROVE=1</c>: the received content is written straight to the
/// approved file and the assertion passes. Review the diff before committing.
/// </summary>
internal static class Approvals
{
    internal static void Verify(
        string received,
        [CallerFilePath] string testFilePath = "",
        [CallerMemberName] string testName = "")
    {
        string sourceFileName = Path.GetFileNameWithoutExtension(testFilePath);
        string snapshotDirectory = Path.Combine(Path.GetDirectoryName(testFilePath)!, "Snapshots");
        Directory.CreateDirectory(snapshotDirectory);

        string baseName = $"{sourceFileName}.{testName}";
        string approvedPath = Path.Combine(snapshotDirectory, baseName + ".verified.txt");
        string receivedPath = Path.Combine(snapshotDirectory, baseName + ".received.txt");

        // Normalize line endings so approved files compare equal across platforms.
        received = received.Replace("\r\n", "\n");

        if (string.Equals(Environment.GetEnvironmentVariable("VERIFY_APPROVE"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(approvedPath, received);
            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }
            Assert.Pass($"Approved snapshot written: {approvedPath}");
        }

        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(receivedPath, received);
            Assert.Fail(
                $"No approved snapshot at {approvedPath}. " +
                $"Reviewed the received output at {receivedPath} and rename it to *.verified.txt, " +
                $"or re-run with VERIFY_APPROVE=1.");
        }

        string approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");
        if (string.Equals(approved, received, StringComparison.Ordinal))
        {
            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }
            return;
        }

        File.WriteAllText(receivedPath, received);
        Assert.That(received, Is.EqualTo(approved),
            $"Snapshot mismatch. Received written to {receivedPath}.");
    }
}
