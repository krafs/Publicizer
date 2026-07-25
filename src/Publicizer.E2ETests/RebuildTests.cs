using NUnit.Framework;

namespace Publicizer.E2ETests;

// The rest of the suite only ever builds a consumer once, from clean. Visual Studio builds
// the same project over and over in one session, against publicized assemblies cached in
// the intermediate folder and against a build node that never goes away — which is where
// stale-cache and file-locking failures live.
public class RebuildTests
{
    private const string PrivateClassPrintingFoo = """
        namespace PrivateNamespace;
        class PrivateClass
        {
            private string PrivateField = "foo";
        }
        """;

    private const string AppCode = "System.Console.Write(new PrivateNamespace.PrivateClass().PrivateField);";

    private static TestProject Consumer(TestProject library) => TestProject.App(AppCode)
        .Referencing(library)
        .ConsumingPublicizer()
        .Item("Publicize", "PrivateAssembly");

    [Test]
    public void RebuildingAnUnchangedConsumer_StillBuildsAndRuns()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PrivateClassPrintingFoo).BuildOrFail();
        using TestProject app = Consumer(library);

        Assert.That(app.Build().ExitCode, Is.Zero);

        ProcessResult rebuildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(rebuildResult.ExitCode, Is.Zero, rebuildResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foo"), runResult.Output);
    }

    [Test]
    public void RebuildingAfterTheReferencedAssemblyChanged_PublicizesTheNewOne()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PrivateClassPrintingFoo).BuildOrFail();
        using TestProject app = Consumer(library);

        Assert.That(app.Build().ExitCode, Is.Zero);

        // A new member, so the rebuilt app only compiles if the cached publicized assembly
        // was replaced rather than reused.
        library.Rewrite("""
            namespace PrivateNamespace;
            class PrivateClass
            {
                private string PrivateField = "foo";
                private string PrivateMethodAddedLater() => "bar";
            }
            """).BuildOrFail();

        app.Rewrite("System.Console.Write(new PrivateNamespace.PrivateClass().PrivateMethodAddedLater());");

        ProcessResult rebuildResult = app.Build();
        ProcessResult runResult = app.Run();

        Assert.That(rebuildResult.ExitCode, Is.Zero, rebuildResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("bar"), runResult.Output);
    }

    [Test]
    public void RebuildingThroughAReusedBuildNode_StillBuildsAndRuns()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PrivateClassPrintingFoo).BuildOrFail();
        using TestProject app = Consumer(library);

        Assert.That(app.BuildReusingNodes().ExitCode, Is.Zero);

        // The second build lands on the node the first one left running, with the task
        // assembly already loaded and the publicized assembly possibly still held open.
        ProcessResult rebuildResult = app.BuildReusingNodes();
        ProcessResult runResult = app.Run();

        Assert.That(rebuildResult.ExitCode, Is.Zero, rebuildResult.Output);
        Assert.That(runResult.Output, Is.EqualTo("foo"), runResult.Output);
    }
}
