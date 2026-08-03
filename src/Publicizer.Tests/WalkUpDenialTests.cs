using dnlib.DotNet;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// The declaring-type walk-up is the engine's only transitive rule: publicizing a nested type also
/// publicizes every type enclosing it, because a nested type is unreachable otherwise. That
/// inference must not undo an explicit <c>DoNotPublicize</c> on an encloser.
/// Uses its own fixture because the shared one has no internal type with a nested type — the
/// enclosers in Shapes are already public, so they cannot show the difference.
/// </summary>
internal static class WalkUpDenialTests
{
    private const string DenialSource =
        """
        namespace Fixture
        {
            internal class Outer
            {
                private int OuterPrivateField;

                private class Nested
                {
                    private int NestedPrivateField;
                }
            }
        }
        """;

    private static readonly byte[] denialAssembly = Compiler.Compile(DenialSource, "Denial");

    private static ModuleDefMD LoadDenialModule() => ModuleDefMD.Load(denialAssembly);

    private static TypeDef Type(ModuleDef module, string reflectionName) => module.Find(reflectionName, isReflectionName: true);

    private static void Publicize(ModuleDef module, PublicizerAssemblyContext context) =>
        PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

    [Test]
    public static void DoNotPublicizeType_SurvivesTheWalkUpFromItsNestedTypes()
    {
        using ModuleDefMD module = LoadDenialModule();
        var context = new PublicizerAssemblyContext("Denial") { ExplicitlyPublicizeAssembly = true };
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Outer");

        Publicize(module, context);

        // The exclusion still does not reach the nested type, which has its own reflection name...
        Assert.That(Type(module, "Fixture.Outer+Nested").IsNestedPublic, Is.True);
        // ...but publicizing it no longer drags the excluded encloser public along with it.
        Assert.That(Type(module, "Fixture.Outer").IsNotPublic, Is.True);
    }

    [Test]
    public static void ExplicitMemberPublicize_StillPublicizesTheDeniedTypeItLivesIn()
    {
        using ModuleDefMD module = LoadDenialModule();
        var context = new PublicizerAssemblyContext("Denial");
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Outer");
        context.PublicizeMemberPatterns.Add("Fixture.Outer.OuterPrivateField");

        Publicize(module, context);

        // The walk-up only stops at *enclosing* types. Naming a member of an excluded type still
        // publicizes that type, or the member just publicized would be unreachable.
        Assert.That(Type(module, "Fixture.Outer").IsPublic, Is.True);
    }

    [Test]
    public static void PublicizeNestedTypeByName_StillWalksUpWhenNothingIsExcluded()
    {
        using ModuleDefMD module = LoadDenialModule();
        var context = new PublicizerAssemblyContext("Denial");
        context.PublicizeMemberPatterns.Add("Fixture.Outer+Nested");

        Publicize(module, context);

        // The walk-up itself is untouched - with no exclusion in play it still makes the
        // nested type reachable by publicizing its encloser.
        Assert.That(Type(module, "Fixture.Outer+Nested").IsNestedPublic, Is.True);
        Assert.That(Type(module, "Fixture.Outer").IsPublic, Is.True);
    }
}
