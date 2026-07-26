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
        // Namespace="AB" and Namespace="A" Type="B" must not collide, or one target's cached
        // assembly would be served for the other.
        var namespaceOnly = new PublicizerAssemblyContext("Fixture");
        namespaceOnly.Scopes.Add(new PublicizeScope { Namespace = "AB" });

        var namespaceAndType = new PublicizerAssemblyContext("Fixture");
        namespaceAndType.Scopes.Add(new PublicizeScope { Namespace = "A", TypeReflectionName = "B" });

        Assert.That(Hash(namespaceAndType), Is.Not.EqualTo(Hash(namespaceOnly)));
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
