using System.IO;

namespace Publicizer.Tests
{
    public static class TestHelper
    {
        static TestHelper()
        {
            TestRootPath = Path.Combine(Path.GetTempPath(), TestConstants.TestRootDirectoryName, Path.GetRandomFileName());
            Directory.CreateDirectory(TestRootPath);
        }

        public static string TestRootPath { get; }

        public static string CreateRandomTestDirectory()
        {
            string path = Path.Combine(TestRootPath, Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetRandomProjectFilePath()
        {
            return Path.Combine(CreateRandomTestDirectory(), Path.GetRandomFileName() + ".csproj");
        }
    }
}
