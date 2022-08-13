using System;
using System.IO;

namespace Publicizer.Tests;

internal sealed class TemporaryFolder : IDisposable
{
    private readonly DirectoryInfo _directoryInfo;
    internal TemporaryFolder()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());

        _directoryInfo = Directory.CreateDirectory(path);
    }

    internal string Path => _directoryInfo.FullName;

    public override string ToString()
    {
        return _directoryInfo.FullName;
    }

    void IDisposable.Dispose()
    {
        _directoryInfo.Delete(recursive: true);
    }
}
