using System;
using System.IO;
using NUnit.Framework;

namespace Publicizer.E2ETests;

// Visual Studio drives IntelliSense from design-time builds: ResolveReferences only, no
// compiler invocation, with DesignTimeBuild set. If publicization doesn't happen there, the
// editor resolves the original assembly and marks every non-public member as inaccessible —
// red squiggles over code that compiles fine from the command line. Nothing else in the
// suite runs a build in that mode.
public class DesignTimeBuildTests
{
    // The properties the .NET Project System sets for a design-time build.
    private static readonly string[] DesignTimeBuildArguments =
    [
        "-t:DumpReferencePaths",
        "-p:DesignTimeBuild=true",
        "-p:SkipCompilerExecution=true",
        "-p:ProvideCommandLineArgs=true",
        "-p:DesignTimeSilentResolution=true"
    ];

    // ResolveReferences is what the design-time targets depend on, and where Publicizer
    // swaps the reference; dumping ReferencePath afterwards is how we see what the editor
    // would have been handed.
    private const string DumpTarget = """
          <Target Name="DumpReferencePaths" DependsOnTargets="ResolveReferences">
            <WriteLinesToFile File="$(MSBuildProjectDirectory)/referencepaths.txt" Lines="@(ReferencePath)" Overwrite="true" WriteOnlyWhenDifferent="false" />
          </Target>
        """;

    [Test]
    public void DesignTimeBuild_ResolvesThePublicizedAssemblyRatherThanTheOriginal()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", PublicizerTests.PrivateClassIn("PrivateNamespace")).BuildOrFail();

        using TestProject app = TestProject.App("System.Console.Write(new PrivateNamespace.PrivateClass().PrivateField);")
            .Referencing(library)
            .ConsumingPublicizer()
            .Item("Publicize", "PrivateAssembly")
            .RawXml(DumpTarget);

        ProcessResult result = app.Build(DesignTimeBuildArguments);
        Assert.That(result.ExitCode, Is.Zero, result.Output);

        string[] referencePaths = File.ReadAllLines(Path.Combine(app.Folder, "referencepaths.txt"));
        string[] privateAssemblyPaths = Array.FindAll(referencePaths, path => path.EndsWith("PrivateAssembly.dll", StringComparison.OrdinalIgnoreCase));

        Assert.That(privateAssemblyPaths, Has.Length.EqualTo(1), result.Output);
        Assert.That(privateAssemblyPaths[0], Does.Contain("PublicizedAssemblies"), result.Output);
    }
}
