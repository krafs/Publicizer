using dnlib.DotNet;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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

            internal class Deep
            {
                private class Mid
                {
                    private class Leaf
                    {
                        private int LeafPrivateField;
                    }
                }
            }
        }
        """;

    private static readonly byte[] denialAssembly = Compiler.Compile(DenialSource, "Denial");

    private static ModuleDefMD LoadDenialModule() => ModuleDefMD.Load(denialAssembly);

    private static TypeDef Type(ModuleDef module, string reflectionName) => module.Find(reflectionName, isReflectionName: true);

    private static void Publicize(ModuleDef module, PublicizerAssemblyContext context) =>
        PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

    private static TaskItem Item(string spec, params string[] metadata)
    {
        var item = new TaskItem(spec);
        for (int i = 0; i < metadata.Length; i += 2)
        {
            item.SetMetadata(metadata[i], metadata[i + 1]);
        }

        return item;
    }

    /// <summary>
    /// Builds the context from real items rather than by setting fields, so these tests cover the
    /// structured form as authored - the parse is where the two item forms diverge.
    /// </summary>
    private static PublicizerAssemblyContext Parse(ITaskItem[] publicizes, ITaskItem[] doNotPublicizes)
    {
        bool valid = PublicizeAssemblies.TryGetPublicizerAssemblyContexts(publicizes, doNotPublicizes, NullTaskLogger.Instance, out Dictionary<string, PublicizerAssemblyContext> contexts);
        Assert.That(valid, Is.True, "expected every item to parse");
        return contexts["Denial"];
    }

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

    /// <summary>
    /// The gate is a type named by name, and the structured form names types too. Saying the same
    /// thing in the two syntaxes has to produce the same assembly, so this is the structured twin of
    /// <see cref="DoNotPublicizeType_SurvivesTheWalkUpFromItsNestedTypes"/>.
    /// </summary>
    [Test]
    public static void StructuredDoNotPublicizeTypeScope_SurvivesTheWalkUpFromItsNestedTypes()
    {
        using ModuleDefMD module = LoadDenialModule();
        PublicizerAssemblyContext context = Parse(
            [Item("Denial:Fixture.Outer+Nested")],
            [Item("Denial", "Namespace", "Fixture", "Type", "Outer")]);

        Publicize(module, context);

        Assert.That(Type(module, "Fixture.Outer+Nested").IsNestedPublic, Is.True);
        Assert.That(Type(module, "Fixture.Outer").IsNotPublic, Is.True);
    }

    /// <summary>
    /// A namespace scope names no type, so it is a sweep rather than a statement about any one type
    /// - and the walk-up is only inference too. Between the two, reachability wins: stopping here
    /// would leave the explicitly named type public but unreachable, defeating the carve-out for a
    /// result nobody can use. A type scope is the way to say "not this type".
    /// </summary>
    [Test]
    public static void StructuredDoNotPublicizeNamespaceScope_DoesNotStopTheWalkUp()
    {
        using ModuleDefMD module = LoadDenialModule();
        PublicizerAssemblyContext context = Parse(
            [Item("Denial:Fixture.Outer+Nested")],
            [Item("Denial", "Namespace", "Fixture")]);

        Publicize(module, context);

        Assert.That(Type(module, "Fixture.Outer+Nested").IsNestedPublic, Is.True);
        Assert.That(Type(module, "Fixture.Outer").IsPublic, Is.True);
    }

    /// <summary>
    /// The gate is name equality, not scope coverage: a deny on <c>Deep</c> sweeps <c>Deep.Mid</c>
    /// but does not name it, so the walk publicizes Mid and stops at Deep. That leaves Mid and Leaf
    /// public but unreachable - an accepted end state, the same one the colon form already produces,
    /// and the price of letting an explicit name be absolute.
    /// </summary>
    [Test]
    public static void StructuredDoNotPublicizeTypeScope_StopsOnlyAtTheTypeItNames()
    {
        using ModuleDefMD module = LoadDenialModule();
        PublicizerAssemblyContext context = Parse(
            [Item("Denial:Fixture.Deep+Mid+Leaf")],
            [Item("Denial", "Namespace", "Fixture", "Type", "Deep")]);

        Publicize(module, context);

        Assert.That(Type(module, "Fixture.Deep+Mid+Leaf").IsNestedPublic, Is.True);
        Assert.That(Type(module, "Fixture.Deep+Mid").IsNestedPublic, Is.True);
        Assert.That(Type(module, "Fixture.Deep").IsNotPublic, Is.True);
    }
}
