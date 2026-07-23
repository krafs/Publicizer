using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes <see cref="TaskItemExtensions"/>: metadata parsing and its
/// default-to-true / null-on-blank behavior.
/// </summary>
public class TaskItemExtensionsTests
{
    [Test]
    public void IncludeCompilerGeneratedMembers_MissingMetadata_DefaultsToTrue()
    {
        var item = new TaskItem("Asm");
        Assert.That(item.IncludeCompilerGeneratedMembers(), Is.True);
    }

    [Test]
    public void IncludeCompilerGeneratedMembers_GarbageMetadata_DefaultsToTrue()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("IncludeCompilerGeneratedMembers", "notabool");
        Assert.That(item.IncludeCompilerGeneratedMembers(), Is.True);
    }

    [Test]
    public void IncludeCompilerGeneratedMembers_False_IsFalse()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("IncludeCompilerGeneratedMembers", "false");
        Assert.That(item.IncludeCompilerGeneratedMembers(), Is.False);
    }

    [Test]
    public void IncludeVirtualMembers_MissingMetadata_DefaultsToTrue()
    {
        var item = new TaskItem("Asm");
        Assert.That(item.IncludeVirtualMembers(), Is.True);
    }

    [Test]
    public void IncludeVirtualMembers_False_IsFalse()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("IncludeVirtualMembers", "false");
        Assert.That(item.IncludeVirtualMembers(), Is.False);
    }

    [Test]
    public void MemberPattern_MissingMetadata_IsNull()
    {
        var item = new TaskItem("Asm");
        Assert.That(item.MemberPattern(), Is.Null);
    }

    [Test]
    public void MemberPattern_BlankMetadata_IsNull()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("MemberPattern", "   ");
        Assert.That(item.MemberPattern(), Is.Null);
    }

    [Test]
    public void MemberPattern_Set_ReturnsMatchingRegex()
    {
        var item = new TaskItem("Asm");
        item.SetMetadata("MemberPattern", ".*Foo.*");

        Regex? pattern = item.MemberPattern();

        Assert.That(pattern, Is.Not.Null);
        Assert.That(pattern!.IsMatch("Namespace.Type.FooBar"), Is.True);
        Assert.That(pattern.IsMatch("Namespace.Type.Bar"), Is.False);
    }

    [Test]
    public void FileName_ReturnsFileNameWithoutExtension()
    {
        var item = new TaskItem("/some/dir/PrivateAssembly.dll");
        Assert.That(item.FileName(), Is.EqualTo("PrivateAssembly"));
    }
}
