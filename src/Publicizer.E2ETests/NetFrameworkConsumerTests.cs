using NUnit.Framework;

namespace Publicizer.E2ETests;

// Every other consumer in the suite targets .NET. A .NET Framework consumer resolves
// references through different search paths and reference assemblies, and gets the
// IgnoresAccessChecksTo attribute compiled against a different corlib — a path Publicizer
// supports and nothing exercised. Windows-only: building net472 needs the targeting pack.
internal static class NetFrameworkConsumerTests
{
    private const string NetFrameworkTargetFramework = "net472";

    [SetUp]
    public static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore(".NET Framework targeting packs are Windows-only.");
        }
    }

    [Test]
    public static void NetFrameworkConsumer_CompilesAndRunsAgainstAPublicizedReference()
    {
        string libraryCode = """
            namespace PrivateNamespace
            {
                class PrivateClass
                {
                    private string PrivateField = "foo";
                    private string PrivateMethod() => "bar";
                }
            }
            """;

        using TestProject library = TestProject.Library("PrivateAssembly", libraryCode)
            .TargetingFramework(NetFrameworkTargetFramework)
            .BuildOrFail();

        string appCode = """
            namespace App
            {
                static class Program
                {
                    static void Main()
                    {
                        var privateClass = new PrivateNamespace.PrivateClass();
                        System.Console.Write(privateClass.PrivateField + privateClass.PrivateMethod());
                    }
                }
            }
            """;

        using TestProject app = TestProject.App(appCode)
            .TargetingFramework(NetFrameworkTargetFramework)
            .Referencing(library)
            .ConsumingPublicizer()
            .Item("Publicize", "PrivateAssembly");

        ProcessResult buildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(buildResult.ExitCode, Is.Zero, buildResult.Output);
        Assert.That(runResult.ExitCode, Is.Zero, runResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foobar"), runResult.Output);
    }
}
