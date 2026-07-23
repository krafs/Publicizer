using System.Linq;
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
public partial class PublicizeAssemblyCharacterizationTests
{
    [GeneratedRegex("Protected")]
    private static partial Regex ProtectedPattern();

    private static bool Publicize(ModuleDef module, PublicizerAssemblyContext context) => PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

    private static FieldDef Field(ModuleDef module, string typeReflectionName, string fieldName) =>
        module.Find(typeReflectionName, isReflectionName: true).Fields.Single(f => f.Name == fieldName);

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
            PublicizeMemberRegexPattern = ProtectedPattern(),
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

    [Test]
    public void PublicizeType_ByName_PublicizesTypeAndWalksUp()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes+Inner");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    [Test]
    public void WholeAssembly_ExceptType_LeavesThatTypesMembersUntouched()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes");

        Publicize(module, context);

        Approvals.Verify(AccessibilityManifest.Of(module));
    }

    // --- Event backing field: the original reason for the compiler-generated filter (issue #9). ---

    [Test]
    public void EventBackingField_WholeAssemblyDefault_BecomesPublic_TheCollision()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };

        Publicize(module, context);

        // Backing field goes public alongside the public event of the same name — the CS0229 case.
        Assert.That(Field(module, "Fixture.Shapes", "FieldLikeEvent").IsPublic, Is.True);
    }

    [Test]
    public void EventBackingField_ExcludingCompilerGenerated_StaysPrivate()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture")
        {
            ExplicitlyPublicizeAssembly = true,
            IncludeCompilerGeneratedMembers = false,
        };

        Publicize(module, context);

        Assert.That(Field(module, "Fixture.Shapes", "FieldLikeEvent").IsPrivate, Is.True);
    }

    // --- Precedence and generics. ---

    [Test]
    public void MemberInBothPublicizeAndDoNotPublicize_DoNotPublicizeWins()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Publicize(module, context);

        Assert.That(Field(module, "Fixture.Shapes", "PrivateField").IsPrivate, Is.True);
    }

    [Test]
    public void SingleMember_GenericField_MatchesArityMangledName()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.GenericHolder`1.GenericField");

        Publicize(module, context);

        Assert.That(Field(module, "Fixture.GenericHolder`1", "GenericField").IsPublic, Is.True);
    }

    [Test]
    public void PublicizeTarget_MatchesNothing_PublicizesNothingAndReturnsFalse()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.NoSuchMember");

        // Silent no-match: nothing is publicized and no per-target diagnostic is raised here
        // (the assembly-level warning lives in the task; see ExecuteTests). The rewrite's strict
        // mode intends to change this deliberately.
        bool modified = Publicize(module, context);

        Assert.That(modified, Is.False);
        Assert.That(Field(module, "Fixture.Shapes", "PrivateField").IsPrivate, Is.True);
    }

    [Test]
    public void ExplicitMemberPublicize_BeatsDoNotPublicizeType()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture");
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Publicize(module, context);

        // The explicit member wins over the type-wide exclusion...
        Assert.That(Field(module, "Fixture.Shapes", "PrivateField").IsPublic, Is.True);
        // ...but other members of the excluded type stay untouched.
        Assert.That(Field(module, "Fixture.Shapes", "ProtectedField").IsFamily, Is.True);
    }

    [Test]
    public void DoNotPublicizeEvent_ByName_ExcludesBackingField()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.FieldLikeEvent");

        Publicize(module, context);

        // Events aren't matched as first-class members, but the documented workaround (issue #141)
        // works by coincidence: the backing field shares the event's name, so DoNotPublicize-ing the
        // event name excludes the field and avoids the CS0229 collision.
        Assert.That(Field(module, "Fixture.Shapes", "FieldLikeEvent").IsPrivate, Is.True);
        // The rest of the assembly is still publicized.
        Assert.That(Field(module, "Fixture.Shapes", "PrivateField").IsPublic, Is.True);
    }
}
