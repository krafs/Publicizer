using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes <see cref="PublicizeAssemblies.GetPublicizerAssemblyContexts"/>:
/// how Publicize / DoNotPublicize item specs and metadata parse into per-assembly
/// contexts.
/// </summary>
public class GetPublicizerAssemblyContextsTests
{
    private static Dictionary<string, PublicizerAssemblyContext> Build(ITaskItem[] publicizes, ITaskItem[]? doNotPublicizes = null) =>
        PublicizeAssemblies.GetPublicizerAssemblyContexts(publicizes, doNotPublicizes ?? System.Array.Empty<ITaskItem>(), NullTaskLogger.Instance);

    [Test]
    public void AssemblyWidePublicize_SetsExplicitlyPublicizeAssemblyWithDefaults()
    {
        Dictionary<string, PublicizerAssemblyContext> contexts = Build(new ITaskItem[] { new TaskItem("Asm") });

        Assert.That(contexts.ContainsKey("Asm"), Is.True);
        PublicizerAssemblyContext context = contexts["Asm"];
        Assert.That(context.ExplicitlyPublicizeAssembly, Is.True);
        Assert.That(context.IncludeCompilerGeneratedMembers, Is.True);
        Assert.That(context.IncludeVirtualMembers, Is.True);
        Assert.That(context.PublicizeMemberPatterns, Is.Empty);
    }

    [Test]
    public void MemberPublicize_AddsMemberPatternAndDoesNotPublicizeWholeAssembly()
    {
        Dictionary<string, PublicizerAssemblyContext> contexts =
            Build(new ITaskItem[] { new TaskItem("Asm:Ns.Type.Member") });

        PublicizerAssemblyContext context = contexts["Asm"];
        Assert.That(context.ExplicitlyPublicizeAssembly, Is.False);
        Assert.That(context.PublicizeMemberPatterns, Does.Contain("Ns.Type.Member"));
    }

    [Test]
    public void AssemblyWidePublicize_HonorsIncludeFlagMetadata()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("IncludeVirtualMembers", "false");
        item.SetMetadata("IncludeCompilerGeneratedMembers", "false");

        PublicizerAssemblyContext context = Build(new ITaskItem[] { item })["Asm"];

        Assert.That(context.IncludeVirtualMembers, Is.False);
        Assert.That(context.IncludeCompilerGeneratedMembers, Is.False);
    }

    [Test]
    public void AssemblyWidePublicize_HonorsMemberPatternMetadata()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("MemberPattern", ".*Foo.*");

        PublicizerAssemblyContext context = Build(new ITaskItem[] { item })["Asm"];

        Assert.That(context.PublicizeMemberRegexPattern, Is.Not.Null);
    }

    [Test]
    public void DoNotPublicizeAssembly_SetsExplicitlyDoNotPublicizeAssembly()
    {
        Dictionary<string, PublicizerAssemblyContext> contexts =
            Build(System.Array.Empty<ITaskItem>(), new ITaskItem[] { new TaskItem("Asm") });

        Assert.That(contexts["Asm"].ExplicitlyDoNotPublicizeAssembly, Is.True);
    }

    [Test]
    public void DoNotPublicizeMember_AddsDoNotPublicizeMemberPattern()
    {
        Dictionary<string, PublicizerAssemblyContext> contexts =
            Build(System.Array.Empty<ITaskItem>(), new ITaskItem[] { new TaskItem("Asm:Ns.Type.Member") });

        Assert.That(contexts["Asm"].DoNotPublicizeMemberPatterns, Does.Contain("Ns.Type.Member"));
    }

    [Test]
    public void SameAssemblyInPublicizeAndDoNotPublicize_MergesIntoOneContext()
    {
        Dictionary<string, PublicizerAssemblyContext> contexts = Build(new ITaskItem[] { new TaskItem("Asm") }, new ITaskItem[] { new TaskItem("Asm:Ns.Type.Member") });

        Assert.That(contexts, Has.Count.EqualTo(1));
        PublicizerAssemblyContext context = contexts["Asm"];
        Assert.That(context.ExplicitlyPublicizeAssembly, Is.True);
        Assert.That(context.DoNotPublicizeMemberPatterns, Does.Contain("Ns.Type.Member"));
    }
}
