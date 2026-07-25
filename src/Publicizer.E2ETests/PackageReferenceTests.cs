using NUnit.Framework;

namespace Publicizer.E2ETests;

// Every other consumer in the suite reaches the private assembly through a
// <Reference HintPath=...>. The common real-world case is a PackageReference: the assembly
// arrives on ReferencePath from the NuGet restore graph, carrying package metadata and
// different copy-local semantics, and the reference Publicizer swaps in has to survive that.
public class PackageReferenceTests
{
    private const string PrivateClassCode = """
        namespace PrivateNamespace;
        class PrivateClass
        {
            private string PrivateField = "foo";
            private string PrivateMethod() => "bar";
        }
        """;

    private const string AppCode = """
        var privateClass = new PrivateNamespace.PrivateClass();
        System.Console.Write(privateClass.PrivateField + privateClass.PrivateMethod());
        """;

    [Test]
    public void PublicizeAssemblyFromAPackageReference_CompilesAndRuns()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PrivateClassCode).PackOrFail();

        using TestProject app = TestProject.App(AppCode)
            .ReferencingPackage(library)
            .ConsumingPublicizer()
            .Item("Publicize", "PrivateAssembly");

        ProcessResult buildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(buildResult.ExitCode, Is.Zero, buildResult.Output);
        Assert.That(runResult.ExitCode, Is.Zero, runResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foobar"), runResult.Output);
    }
}
