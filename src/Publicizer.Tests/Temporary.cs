using System;
using System.IO;

namespace Publicizer.Tests;

/// <summary>
/// Utility class for creating temporary folders.
/// </summary>
internal static class Temporary
{
    internal static string NewFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        return Directory.CreateDirectory(path).FullName;
    }
}
