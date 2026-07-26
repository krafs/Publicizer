using dnlib.DotNet;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes what publicization does *not* do: it changes the accessibility of the members it
/// matches by name, and nothing else. Types appearing in a publicized member's signature are not
/// pulled along, so a member can become public while remaining unusable from consuming code.
/// Uses its own fixture rather than the shared one so these shapes don't churn every snapshot.
/// </summary>
internal static class SignatureClosureTests
{
    // A public type whose private members traffic in types the consumer cannot name, plus a
    // global-namespace type (issue #14) whose reflection name carries no namespace prefix.
    private const string ClosureSource =
        """
        internal class GlobalHiddenType
        {
            private int GlobalPrivateField;
        }

        namespace Fixture
        {
            internal class HiddenType { }

            public class Api
            {
                private HiddenType ReturnsHidden() => new HiddenType();
                private void TakesHidden(HiddenType hidden) { }
                private HiddenType HiddenField;
            }
        }
        """;

    private static readonly byte[] closureAssembly = Compiler.Compile(ClosureSource, "Closure");

    private static ModuleDefMD LoadClosureModule() => ModuleDefMD.Load(closureAssembly);

    private static TypeDef Type(ModuleDef module, string reflectionName) => module.Find(reflectionName, isReflectionName: true);

    private static MethodDef Method(ModuleDef module, string typeReflectionName, string methodName) =>
        Type(module, typeReflectionName).Methods.Single(m => m.Name == methodName);

    private static void Publicize(ModuleDef module, PublicizerAssemblyContext context) => PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

    [Test]
    public static void PublicizingMethod_DoesNotPublicizeItsReturnType()
    {
        using ModuleDefMD module = LoadClosureModule();
        var context = new PublicizerAssemblyContext("Closure");
        context.PublicizeMemberPatterns.Add("Fixture.Api.ReturnsHidden");

        Publicize(module, context);

        Assert.That(Method(module, "Fixture.Api", "ReturnsHidden").IsPublic, Is.True);
        // The return type stays internal. Callers can still use `var`, so this one is survivable.
        Assert.That(Type(module, "Fixture.HiddenType").IsNotPublic, Is.True);
    }

    [Test]
    public static void PublicizingMethod_DoesNotPublicizeItsParameterTypes()
    {
        using ModuleDefMD module = LoadClosureModule();
        var context = new PublicizerAssemblyContext("Closure");
        context.PublicizeMemberPatterns.Add("Fixture.Api.TakesHidden");

        Publicize(module, context);

        // The method is public but uncallable: the caller cannot name a value of the parameter type.
        Assert.That(Method(module, "Fixture.Api", "TakesHidden").IsPublic, Is.True);
        Assert.That(Type(module, "Fixture.HiddenType").IsNotPublic, Is.True);
    }

    [Test]
    public static void PublicizingField_DoesNotPublicizeItsFieldType()
    {
        using ModuleDefMD module = LoadClosureModule();
        var context = new PublicizerAssemblyContext("Closure");
        context.PublicizeMemberPatterns.Add("Fixture.Api.HiddenField");

        Publicize(module, context);

        Assert.That(Type(module, "Fixture.Api").Fields.Single(f => f.Name == "HiddenField").IsPublic, Is.True);
        Assert.That(Type(module, "Fixture.HiddenType").IsNotPublic, Is.True);
    }

    [Test]
    public static void WholeAssembly_ClosesOverSignatureTypesByAccident()
    {
        using ModuleDefMD module = LoadClosureModule();
        var context = new PublicizerAssemblyContext("Closure") { ExplicitlyPublicizeAssembly = true };

        Publicize(module, context);

        // Whole-assembly mode has no closure problem because it publicizes everything anyway.
        // The gap only bites targeted publicization, which is why it has gone largely unreported.
        Assert.That(Type(module, "Fixture.HiddenType").IsPublic, Is.True);
    }

    [Test]
    public static void GlobalNamespaceType_IsNamedWithoutNamespacePrefix()
    {
        using ModuleDefMD module = LoadClosureModule();
        var context = new PublicizerAssemblyContext("Closure");
        context.PublicizeMemberPatterns.Add("GlobalHiddenType.GlobalPrivateField");

        Publicize(module, context);

        Assert.That(Type(module, "GlobalHiddenType").Fields.Single(f => f.Name == "GlobalPrivateField").IsPublic, Is.True);
    }
}
