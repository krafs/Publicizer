using System.Text.RegularExpressions;
using dnlib.DotNet;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes the full <see cref="PublicizeAssemblies.PublicizeAssembly"/> decision
/// tree by snapshotting the resulting accessibility manifest of the fixture module for
/// each scenario. These snapshots freeze current behavior so the tree can be refactored
/// safely: any change to a member's resulting visibility surfaces as a snapshot diff.
/// </summary>
public class PublicizeAssemblyCharacterizationTests
{
    private static bool Publicize(ModuleDef module, PublicizerAssemblyContext context) =>
        PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

    [Test]
    public void WholeAssembly_Defaults()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };

        bool modified = Publicize(module, context);

        Assert.That(modified, Is.True);
        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void WholeAssembly_ExcludingVirtualMembers()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture")
        {
            ExplicitlyPublicizeAssembly = true,
            IncludeVirtualMembers = false,
        };

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void WholeAssembly_ExcludingCompilerGeneratedMembers()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture")
        {
            ExplicitlyPublicizeAssembly = true,
            IncludeCompilerGeneratedMembers = false,
        };

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void WholeAssembly_WithMemberRegexPattern()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture")
        {
            ExplicitlyPublicizeAssembly = true,
            PublicizeMemberRegexPattern = new Regex("Protected"),
        };

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void SingleMember_Field()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void SingleMember_Property()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateAutoProp");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void SingleMember_Method()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateMethod");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void SingleMember_Constructor()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes..ctor");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void WholeAssembly_ExceptOneProperty_LeavesAccessorsUntouched()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.PrivateAutoProp");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void DoNotPublicizeAssembly_PublicizesNothing()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyDoNotPublicizeAssembly = true };

        bool modified = Publicize(module, context);

        Assert.That(modified, Is.False);
        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void NestedMember_AlsoPublicizesEnclosingType()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes+Inner.InnerPrivateField");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }
}
