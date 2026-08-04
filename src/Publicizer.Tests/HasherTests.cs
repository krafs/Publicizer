using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes <see cref="Hasher"/>: the cache-key contract. The hash must be
/// stable for equal inputs and must change when any input that affects
/// publicization output changes.
/// </summary>
internal static partial class HasherTests
{
    [GeneratedRegex(".*Foo.*")]
    private static partial Regex FooPattern();

    private static string Hash(PublicizerAssemblyContext context) => Hasher.ComputeHash(Fixtures.ShapesPath(), context);

    [Test]
    public static void ComputeHash_SameInputs_IsStable()
    {
        string first = Hash(new PublicizerAssemblyContext("Fixture"));
        string second = Hash(new PublicizerAssemblyContext("Fixture"));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public static void ComputeHash_DifferentAssemblyName_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        string other = Hash(new PublicizerAssemblyContext("Other"));

        Assert.That(other, Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_TogglingIncludeCompilerGeneratedMembers_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { IncludeCompilerGeneratedMembers = false };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_TogglingIncludeVirtualMembers_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { IncludeVirtualMembers = false };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_TogglingExplicitlyPublicizeAssembly_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_TogglingExplicitlyDoNotPublicizeAssembly_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyDoNotPublicizeAssembly = true };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_AddingPublicizeMemberPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_AddingDoNotPublicizeMemberPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture");
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_SettingMemberRegexPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { PublicizeMemberRegexPattern = FooPattern() };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_DistinguishesMemberPatternsThatConcatenateAlike()
    {
        // One target "AB" and the two targets "A" and "B" must not collide: the second build would
        // silently reuse the first build's publicized assembly.
        var single = new PublicizerAssemblyContext("Fixture");
        single.PublicizeMemberPatterns.Add("AB");

        var split = new PublicizerAssemblyContext("Fixture");
        split.PublicizeMemberPatterns.Add("A");
        split.PublicizeMemberPatterns.Add("B");

        Assert.That(Hash(split), Is.Not.EqualTo(Hash(single)));
    }

    [Test]
    public static void ComputeHash_DistinguishesPublicizeFromDoNotPublicizeMemberPattern()
    {
        // The same member named by opposite intents. Both leave every Explicitly* flag false, so
        // the pattern set is the only thing telling them apart: without a per-set tag the
        // deny-only build finds the allow build's file cached and hands the compiler a publicized
        // assembly it explicitly asked not to have.
        var publicize = new PublicizerAssemblyContext("Fixture");
        publicize.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        var doNotPublicize = new PublicizerAssemblyContext("Fixture");
        doNotPublicize.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Assert.That(Hash(doNotPublicize), Is.Not.EqualTo(Hash(publicize)));
    }

    [Test]
    public static void ComputeHash_MemberPatternOrder_DoesNotChangeHash()
    {
        var first = new PublicizerAssemblyContext("Fixture");
        first.PublicizeMemberPatterns.Add("A");
        first.PublicizeMemberPatterns.Add("B");

        var second = new PublicizerAssemblyContext("Fixture");
        second.PublicizeMemberPatterns.Add("B");
        second.PublicizeMemberPatterns.Add("A");

        Assert.That(Hash(second), Is.EqualTo(Hash(first)));
    }

    [Test]
    public static void ComputeHash_AddingScope_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture");
        context.Scopes.Add(new PublicizeScope { Namespace = "Fixture" });

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public static void ComputeHash_DistinguishesScopesThatConcatenateAlike()
    {
        // The namespace "A.B" and the global-namespace type "A.B" must not collide, or one target's
        // cached assembly would be served for the other. They differ only in which field holds the
        // name, so an undelimited concatenation would hash both as "A.B".
        var namespaceScope = new PublicizerAssemblyContext("Fixture");
        namespaceScope.Scopes.Add(new PublicizeScope { Namespace = "A.B" });

        var typeScope = new PublicizerAssemblyContext("Fixture");
        typeScope.Scopes.Add(new PublicizeScope { TypeReflectionName = "A.B" });

        Assert.That(Hash(typeScope), Is.Not.EqualTo(Hash(namespaceScope)));
    }

    [Test]
    public static void ComputeHash_ScopeFilters_ChangeHash()
    {
        var baseline = new PublicizerAssemblyContext("Fixture");
        baseline.Scopes.Add(new PublicizeScope { Namespace = "Fixture" });

        var filtered = new PublicizerAssemblyContext("Fixture");
        filtered.Scopes.Add(new PublicizeScope { Namespace = "Fixture", IncludeVirtualMembers = false });

        Assert.That(Hash(filtered), Is.Not.EqualTo(Hash(baseline)));
    }

    [Test]
    public static void ComputeHash_DenyScope_DiffersFromPublicizeScope()
    {
        var publicize = new PublicizerAssemblyContext("Fixture");
        publicize.Scopes.Add(new PublicizeScope { Namespace = "Fixture" });

        var deny = new PublicizerAssemblyContext("Fixture");
        deny.Scopes.Add(new PublicizeScope { Namespace = "Fixture", Deny = true });

        Assert.That(Hash(deny), Is.Not.EqualTo(Hash(publicize)));
    }
}
