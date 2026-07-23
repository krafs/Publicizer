using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes the <see cref="PublicizeAssemblies"/> task contract itself — output
/// items, on-disk caching, no-op paths, logging — driven in-process with a fake build
/// engine. The member-level publicization behavior is covered by
/// <see cref="PublicizeAssemblyCharacterizationTests"/>.
/// </summary>
public class ExecuteTests
{
    private static PublicizeAssemblies NewTask(string outputDirectory, out FakeBuildEngine engine)
    {
        engine = new FakeBuildEngine();
        return new PublicizeAssemblies
        {
            BuildEngine = engine,
            OutputDirectory = outputDirectory,
            ReferencePaths = [new TaskItem(Fixtures.ShapesPath())],
            Publicizes = [new TaskItem("Fixture")],
        };
    }

    [Test]
    public void Execute_PublicizesReference_PopulatesOutputItemsUnderOutputDirectory()
    {
        using var output = new TemporaryFolder();
        PublicizeAssemblies task = NewTask(output.Path, out _);

        bool result = task.Execute();

        Assert.That(result, Is.True);
        Assert.That(task.ReferencePathsToDelete!.Single().ItemSpec, Is.EqualTo(Fixtures.ShapesPath()));

        string addedPath = task.ReferencePathsToAdd!.Single().ItemSpec;
        Assert.That(File.Exists(addedPath), Is.True);
        Assert.That(addedPath, Does.StartWith(output.Path));
    }

    [Test]
    public void Execute_SecondRunWithSameInputs_IsCacheHit()
    {
        using var output = new TemporaryFolder();

        PublicizeAssemblies first = NewTask(output.Path, out _);
        Assert.That(first.Execute(), Is.True);
        string firstPath = first.ReferencePathsToAdd!.Single().ItemSpec;

        PublicizeAssemblies second = NewTask(output.Path, out FakeBuildEngine secondEngine);
        Assert.That(second.Execute(), Is.True);
        string secondPath = second.ReferencePathsToAdd!.Single().ItemSpec;

        Assert.That(secondPath, Is.EqualTo(firstPath));
        Assert.That(secondEngine.Messages, Has.Some.Contains("already publicized"));
    }

    [Test]
    public void Execute_NoPublicizes_ReturnsTrueWithoutOutputs()
    {
        using var output = new TemporaryFolder();
        var task = new PublicizeAssemblies
        {
            BuildEngine = new FakeBuildEngine(),
            OutputDirectory = output.Path,
            ReferencePaths = [new TaskItem(Fixtures.ShapesPath())],
            Publicizes = [],
        };

        bool result = task.Execute();

        Assert.That(result, Is.True);
        Assert.That(task.ReferencePathsToAdd, Is.Null);
    }

    [Test]
    public void Execute_PublicizeTargetMatchesNothing_WarnsAndDoesNotSwapReference()
    {
        using var output = new TemporaryFolder();
        var engine = new FakeBuildEngine();
        var task = new PublicizeAssemblies
        {
            BuildEngine = engine,
            OutputDirectory = output.Path,
            ReferencePaths = [new TaskItem(Fixtures.ShapesPath())],
            Publicizes = [new TaskItem("Fixture:Fixture.Shapes.NoSuchMember")],
        };

        bool result = task.Execute();

        Assert.That(result, Is.True);
        Assert.That(task.ReferencePathsToAdd, Is.Empty);
        Assert.That(task.ReferencePathsToDelete, Is.Empty);
        Assert.That(engine.Warnings, Has.Some.Contains("no members were publicized"));
    }

    [Test]
    public void Execute_WithLogFilePath_WritesLogFile()
    {
        using var output = new TemporaryFolder();
        string logFilePath = Path.Combine(output.Path, "publicizer.log");

        PublicizeAssemblies task = NewTask(output.Path, out _);
        task.LogFilePath = logFilePath;

        Assert.That(task.Execute(), Is.True);
        Assert.That(File.Exists(logFilePath), Is.True);
    }
}
