namespace Publicizer.E2ETests;

internal sealed class TemporaryFolder : IDisposable
{
    private readonly DirectoryInfo directoryInfo;
    internal TemporaryFolder()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());

        directoryInfo = Directory.CreateDirectory(path);
    }

    internal string Path => directoryInfo.FullName;

    public override string ToString() => directoryInfo.FullName;

    void IDisposable.Dispose() => directoryInfo.Delete(recursive: true);
}
