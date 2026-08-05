using NUnit.Framework;

namespace Publicizer.E2ETests;

// The PUBxxxx codes exist so a consumer can route the warning like any other MSBuild
// warning. Unit tests prove the code reaches the build engine; only a real build proves
// NoWarn and WarningsAsErrors actually route on it.
internal static class DiagnosticSuppressionTests
{
    // Nothing private to publicize, so the task raises PUB3002.
    private const string AllPublicCode = """
        namespace PublicNamespace;
        public class PublicClass
        {
            public string PublicField = "foo";
        }
        """;

    private static TestProject ConsumerOf(TestProject library) => TestProject.App("System.Console.Write(new PublicNamespace.PublicClass().PublicField);")
        .Referencing(library)
        .ConsumingPublicizer()
        .Item("Publicize", "PublicAssembly:PublicNamespace.PublicClass.NoSuchMember");

    [Test]
    public static void Warning_IsReportedWithItsCode()
    {
        using TestProject library = TestProject.Library("PublicAssembly", AllPublicCode).BuildOrFail();
        using TestProject app = ConsumerOf(library);

        ProcessResult result = app.Build();

        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Contain("PUB3002"), result.Output);
    }

    [Test]
    public static void NoWarn_SuppressesTheWarning()
    {
        using TestProject library = TestProject.Library("PublicAssembly", AllPublicCode).BuildOrFail();
        using TestProject app = ConsumerOf(library).Property("NoWarn", "$(NoWarn);PUB3002");

        ProcessResult result = app.Build();

        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Not.Contain("PUB3002"), result.Output);
    }

    [Test]
    public static void WarningsAsErrors_PromotesTheWarningAndFailsTheBuild()
    {
        using TestProject library = TestProject.Library("PublicAssembly", AllPublicCode).BuildOrFail();
        using TestProject app = ConsumerOf(library).Property("WarningsAsErrors", "$(WarningsAsErrors);PUB3002");

        ProcessResult result = app.Build();

        Assert.That(result.ExitCode, Is.Not.Zero, result.Output);
        Assert.That(result.Output, Does.Contain("PUB3002"), result.Output);
    }

    // PUB4001 used to be an error, which failed a build that a consumer whose target assembly is
    // already public at run time had every right to make.
    [Test]
    public static void NoRuntimeStrategy_WarnsWithoutFailingTheBuild()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PublicizerTests.PrivateClassIn("PrivateNamespace")).BuildOrFail();
        using TestProject app = TestProject.App("System.Console.Write(1);")
            .Referencing(library)
            .ConsumingPublicizer()
            .Item("Publicize", "PrivateAssembly")
            .Property("PublicizerRuntimeStrategies", "");

        ProcessResult result = app.Build();

        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Contain("PUB4001"), result.Output);
    }

    // The strategies live in the same target as the publicization, so an unconditioned diagnostic
    // would fire on any project that merely references the package.
    [Test]
    public static void NoRuntimeStrategy_IsSilentWhenNothingWasPublicized()
    {
        using TestProject app = TestProject.App("System.Console.Write(1);")
            .ConsumingPublicizer()
            .Property("PublicizerRuntimeStrategies", "");

        ProcessResult result = app.Build();

        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Not.Contain("PUB4001"), result.Output);
    }
}
