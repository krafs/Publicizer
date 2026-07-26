using NUnit.Framework;

namespace Publicizer.E2ETests;

// Every other consumer in the suite has a single TargetFramework. A consumer with several
// runs one inner build per framework, each publicizing the same reference against a
// different corlib into its own IntermediateOutputPath — so the inner builds must neither
// serve each other a cached assembly nor race over the same output folder.
internal static class MultiTargetedConsumerTests
{
    private const string LegacyTargetFramework = "netstandard2.0";

    private const string LibraryCode = """
        namespace PrivateNamespace
        {
            class PrivateClass
            {
                private string PrivateField = "foo";
            }
        }
        """;

    // A library, not an app: netstandard2.0 has no runtime to run on, and compiling against
    // the private member is already proof that the reference was publicized.
    private const string ConsumerCode = """
        namespace Consumer
        {
            class UsesPrivateMember
            {
                internal string Read() => new PrivateNamespace.PrivateClass().PrivateField;
            }
        }
        """;

    private static TestProject Consumer(TestProject library) => TestProject.Library("Consumer", ConsumerCode)
        .TargetingFrameworks(LegacyTargetFramework, TestProject.DefaultTargetFramework)
        .Referencing(library)
        .ConsumingPublicizer()
        .Item("Publicize", "PrivateAssembly");

    [Test]
    public static void MultiTargetedConsumer_PublicizesInEveryInnerBuild()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", LibraryCode)
            .TargetingFramework(LegacyTargetFramework)
            .BuildOrFail();

        using TestProject consumer = Consumer(library);

        ProcessResult buildResult = consumer.Build();

        Assert.That(buildResult.ExitCode, Is.Zero, buildResult.Output);
        Assert.That(File.Exists(consumer.AssemblyPathFor(LegacyTargetFramework)), Is.True, buildResult.Output);
        Assert.That(File.Exists(consumer.AssemblyPathFor(TestProject.DefaultTargetFramework)), Is.True, buildResult.Output);
    }

    [Test]
    public static void RebuildingAMultiTargetedConsumer_PublicizesTheChangedReferenceInEveryInnerBuild()
    {
        using TestProject library = TestProject.Library("PrivateAssembly", LibraryCode)
            .TargetingFramework(LegacyTargetFramework)
            .BuildOrFail();

        using TestProject consumer = Consumer(library);

        Assert.That(consumer.Build().ExitCode, Is.Zero);

        // A new member, so each inner build only compiles if it replaced its own cached
        // publicized assembly rather than reusing it.
        library.Rewrite("""
            namespace PrivateNamespace
            {
                class PrivateClass
                {
                    private string PrivateField = "foo";
                    private string PrivateMethodAddedLater() => "bar";
                }
            }
            """).BuildOrFail();

        consumer.Rewrite("""
            namespace Consumer
            {
                class UsesPrivateMember
                {
                    internal string Read() => new PrivateNamespace.PrivateClass().PrivateMethodAddedLater();
                }
            }
            """);

        ProcessResult rebuildResult = consumer.Build();

        Assert.That(rebuildResult.ExitCode, Is.Zero, rebuildResult.Output);
    }
}
