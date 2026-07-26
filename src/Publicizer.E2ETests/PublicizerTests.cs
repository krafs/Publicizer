using NUnit.Framework;

namespace Publicizer.E2ETests;

// End-to-end smoke of the MSBuild integration: prove the task loads, the targets run,
// and a real consumer builds and runs against a publicized reference. One case per
// MSBuild path (explicit Publicize items, and PublicizeAll). What publicization does to
// each member kind is covered by the characterization and engine unit tests, not here.
public class PublicizerTests
{
    private const string PrivateClassCode = """
        namespace {0};
        class PrivateClass
        {
            private PrivateClass()
            { }

            private string PrivateField = "foo";
            private string PrivateProperty => "ba";
            private string PrivateMethod() => "r";
        }
        """;

    internal static string PrivateClassIn(string @namespace) => PrivateClassCode.Replace("{0}", @namespace, StringComparison.Ordinal);

    // Fail rather than skip: the builder is set explicitly per CI leg, so a mismatch is a
    // misconfiguration, and a silent skip would let the desktop MSBuild leg stop running
    // without anyone noticing.
    [OneTimeSetUp]
    public void RequireWindowsForDesktopMSBuild()
    {
        if (Runner.UsesDesktopMSBuild && !OperatingSystem.IsWindows())
        {
            Assert.Fail($"PUBLICIZER_TEST_BUILDER={Runner.Builder} requires Windows; desktop MSBuild.exe does not run on this OS.");
        }
    }

    [Test]
    public void PublicizeAssembly_CompilesAndRunsWithExitCode0AndPrintsReturnValuesFromAllPrivateMembersInPrivateClass()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PrivateClassIn("PrivateNamespace")).BuildOrFail();

        string appCode = """
            var privateClass = new PrivateNamespace.PrivateClass();
            var result = privateClass.PrivateField;
            result += privateClass.PrivateProperty;
            result += privateClass.PrivateMethod();
            System.Console.Write(result);
            """;

        using TestProject app = TestProject.App(appCode)
            .Referencing(library)
            .ConsumingPublicizer()
            .Item("Publicize", "PrivateAssembly");

        ProcessResult buildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(buildResult.ExitCode, Is.Zero, buildResult.Output);
        Assert.That(runResult.ExitCode, Is.Zero, runResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foobar"), runResult.Output);
    }

    [Test]
    public void PublicizeAll_CompilesAndRunsWithExitCode0AndPrintsReturnValuesFromPrivateMembersFromTwoDifferentAssemblies()
    {
        using TestProject library1 = TestProject.Library("PrivateAssembly1", PrivateClassIn("PrivateNamespace1")).BuildOrFail();
        using TestProject library2 = TestProject.Library("PrivateAssembly2", PrivateClassIn("PrivateNamespace2")).BuildOrFail();

        string appCode = """
            var privateClass1 = new PrivateNamespace1.PrivateClass();
            var result1 = privateClass1.PrivateField;
            result1 += privateClass1.PrivateProperty;
            result1 += privateClass1.PrivateMethod();

            var privateClass2 = new PrivateNamespace2.PrivateClass();
            var result2 = privateClass2.PrivateField;
            result2 += privateClass2.PrivateProperty;
            result2 += privateClass2.PrivateMethod();

            System.Console.Write(result1 + result2);
            """;

        using TestProject app = TestProject.App(appCode)
            .Referencing(library1)
            .Referencing(library2)
            .ConsumingPublicizer()
            .Property("PublicizeAll", "true");

        ProcessResult buildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(buildResult.ExitCode, Is.Zero, buildResult.Output);
        Assert.That(runResult.ExitCode, Is.Zero, runResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foobarfoobar"), runResult.Output);
    }
}
