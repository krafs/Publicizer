using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes <see cref="Hasher"/>: the cache-key contract. The hash must be
/// stable for equal inputs and must change when any input that affects
/// publicization output changes.
/// </summary>
public class HasherTests
{
    private static string Hash(PublicizerAssemblyContext context) =>
        Hasher.ComputeHash(Fixtures.ShapesPath(), context);

    [Test]
    public void ComputeHash_SameInputs_IsStable()
    {
        string first = Hash(new PublicizerAssemblyContext("Fixture"));
        string second = Hash(new PublicizerAssemblyContext("Fixture"));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ComputeHash_DifferentAssemblyName_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        string other = Hash(new PublicizerAssemblyContext("Other"));

        Assert.That(other, Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_TogglingIncludeCompilerGeneratedMembers_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { IncludeCompilerGeneratedMembers = false };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_TogglingIncludeVirtualMembers_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { IncludeVirtualMembers = false };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_TogglingExplicitlyPublicizeAssembly_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyPublicizeAssembly = true };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_TogglingExplicitlyDoNotPublicizeAssembly_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { ExplicitlyDoNotPublicizeAssembly = true };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_AddingPublicizeMemberPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_AddingDoNotPublicizeMemberPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture");
        context.DoNotPublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void ComputeHash_SettingMemberRegexPattern_ChangesHash()
    {
        string baseline = Hash(new PublicizerAssemblyContext("Fixture"));
        var context = new PublicizerAssemblyContext("Fixture") { PublicizeMemberRegexPattern = new Regex(".*Foo.*") };

        Assert.That(Hash(context), Is.Not.EqualTo(baseline));
    }
}
