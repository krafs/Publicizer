using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Covers <see cref="AssemblyPlan"/> and <see cref="TypePlan"/>: the compiled form of a
/// <see cref="PublicizerAssemblyContext"/>, and the single decision ladder the member walk runs.
/// The ladder itself is pinned end-to-end by <see cref="PublicizeAssemblyCharacterizationTests"/>;
/// these tests exercise the rungs directly, without dnlib.
/// </summary>
internal static partial class AssemblyPlanTests
{
    [GeneratedRegex("Visible")]
    private static partial Regex VisiblePattern();

    private static TypePlan ForType(PublicizerAssemblyContext context, string typeName)
    {
        TypePlan? typePlan = AssemblyPlan.Compile(context).ForType(typeName);
        Assert.That(typePlan, Is.Not.Null, $"expected a plan for {typeName}");
        return typePlan!;
    }

    private static PublicizerAssemblyContext SweepAll() =>
        new("Asm") { ExplicitlyPublicizeAssembly = true };

    [Test]
    public static void ForType_NoRuleCanReachType_ReturnsNull()
    {
        var context = new PublicizerAssemblyContext("Asm");
        context.PublicizeMemberPatterns.Add("Ns.Other.Member");

        Assert.That(AssemblyPlan.Compile(context).ForType("Ns.Type"), Is.Null);
    }

    [Test]
    public static void ForType_DoNotPublicizeAssemblyWithNoNamedTargets_ReturnsNull()
    {
        var context = new PublicizerAssemblyContext("Asm") { ExplicitlyDoNotPublicizeAssembly = true };

        Assert.That(AssemblyPlan.Compile(context).ForType("Ns.Type"), Is.Null);
    }

    [Test]
    public static void ForType_NamedTargetSurvivesDoNotPublicizeAssembly()
    {
        var context = new PublicizerAssemblyContext("Asm") { ExplicitlyDoNotPublicizeAssembly = true };
        context.PublicizeMemberPatterns.Add("Ns.Type.Member");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember("Member", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
    }

    [Test]
    public static void DecideMember_DoNotPublicizeBeatsPublicizeAtSameSpecificity()
    {
        PublicizerAssemblyContext context = SweepAll();
        context.PublicizeMemberPatterns.Add("Ns.Type.Member");
        context.DoNotPublicizeMemberPatterns.Add("Ns.Type.Member");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember("Member", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.DeniedExplicitly));
    }

    [Test]
    public static void DecideMember_ExplicitMemberBeatsDeniedType()
    {
        var context = new PublicizerAssemblyContext("Asm");
        context.DoNotPublicizeMemberPatterns.Add("Ns.Type");
        context.PublicizeMemberPatterns.Add("Ns.Type.Member");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember("Member", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
        Assert.That(typePlan.DecideMember("Other", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void DecideMember_ExplicitBypassesTheSweepFilters()
    {
        PublicizerAssemblyContext context = SweepAll();
        context.IncludeCompilerGeneratedMembers = false;
        context.PublicizeMemberRegexPattern = VisiblePattern();
        context.PublicizeMemberPatterns.Add("Ns.Type.Hidden");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember("Hidden", isCompilerGenerated: true), Is.EqualTo(PublicizeDecision.Explicit));
    }

    [Test]
    public static void DecideMember_SweepAppliesRegexToTheFlatName()
    {
        PublicizerAssemblyContext context = SweepAll();
        context.PublicizeMemberRegexPattern = VisiblePattern();

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember("VisibleMember", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.ByAssemblyRule));
        Assert.That(typePlan.DecideMember("HiddenMember", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void PublicizedTypeTarget_DoesNotReachItsMembers_ButDeniedTypeTargetDoes()
    {
        // The allow/deny asymmetry documented in docs/publicization-semantics.md: naming a type in
        // Publicize publicizes only the type, while naming it in DoNotPublicize also suppresses
        // every member inside it.
        var allow = new PublicizerAssemblyContext("Asm");
        allow.PublicizeMemberPatterns.Add("Ns.Type");
        TypePlan allowPlan = ForType(allow, "Ns.Type");

        Assert.That(allowPlan.DecideType(isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
        Assert.That(allowPlan.DecideMember("Member", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Skip));

        PublicizerAssemblyContext deny = SweepAll();
        deny.DoNotPublicizeMemberPatterns.Add("Ns.Type");
        TypePlan denyPlan = ForType(deny, "Ns.Type");

        Assert.That(denyPlan.DecideType(isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.DeniedExplicitly));
        Assert.That(denyPlan.DecideMember("Member", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void DecideMember_ConstructorTarget_SurvivesTheDoubledDot()
    {
        // "Ns.Type..ctor" splits into ("Ns.Type", ".ctor"), which a split at the last dot would miss.
        var context = new PublicizerAssemblyContext("Asm");
        context.PublicizeMemberPatterns.Add("Ns.Type..ctor");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.DecideMember(".ctor", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
    }

    [Test]
    public static void DecideMember_AmbiguousTarget_MatchesEitherReading()
    {
        // "A.B.C" may name type A.B.C, or member C of type A.B. The syntax cannot say which, so
        // both readings must match — exactly as the flat string comparison did.
        var context = new PublicizerAssemblyContext("Asm");
        context.PublicizeMemberPatterns.Add("A.B.C");

        Assert.That(ForType(context, "A.B").DecideMember("C", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
        Assert.That(ForType(context, "A.B.C").DecideType(isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
        Assert.That(ForType(context, "A").DecideMember("B.C", isCompilerGenerated: false), Is.EqualTo(PublicizeDecision.Explicit));
    }

    [Test]
    public static void TryDecideAllMembers_PlainSweep_IsUniform()
    {
        TypePlan typePlan = ForType(SweepAll(), "Ns.Type");

        Assert.That(typePlan.TryDecideAllMembers(out PublicizeDecision decision), Is.True);
        Assert.That(decision, Is.EqualTo(PublicizeDecision.ByAssemblyRule));
    }

    [Test]
    public static void TryDecideAllMembers_DeniedType_IsUniformlySkipped()
    {
        PublicizerAssemblyContext context = SweepAll();
        context.DoNotPublicizeMemberPatterns.Add("Ns.Type");

        TypePlan typePlan = ForType(context, "Ns.Type");

        Assert.That(typePlan.TryDecideAllMembers(out PublicizeDecision decision), Is.True);
        Assert.That(decision, Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void TryDecideAllMembers_PerMemberFilters_AreNotUniform()
    {
        PublicizerAssemblyContext withRegex = SweepAll();
        withRegex.PublicizeMemberRegexPattern = VisiblePattern();
        Assert.That(ForType(withRegex, "Ns.Type").TryDecideAllMembers(out _), Is.False);

        PublicizerAssemblyContext withoutCompilerGenerated = SweepAll();
        withoutCompilerGenerated.IncludeCompilerGeneratedMembers = false;
        Assert.That(ForType(withoutCompilerGenerated, "Ns.Type").TryDecideAllMembers(out _), Is.False);

        PublicizerAssemblyContext withNamedTarget = SweepAll();
        withNamedTarget.DoNotPublicizeMemberPatterns.Add("Ns.Type.Member");
        Assert.That(ForType(withNamedTarget, "Ns.Type").TryDecideAllMembers(out _), Is.False);
    }

    [Test]
    public static void NeedsCompilerGeneratedCheck_OnlyWhenTheSweepFiltersOnIt()
    {
        PublicizerAssemblyContext filtering = SweepAll();
        filtering.IncludeCompilerGeneratedMembers = false;
        Assert.That(AssemblyPlan.Compile(filtering).NeedsCompilerGeneratedCheck, Is.True);

        Assert.That(AssemblyPlan.Compile(SweepAll()).NeedsCompilerGeneratedCheck, Is.False);

        var namedOnly = new PublicizerAssemblyContext("Asm") { IncludeCompilerGeneratedMembers = false };
        namedOnly.PublicizeMemberPatterns.Add("Ns.Type.Member");
        Assert.That(AssemblyPlan.Compile(namedOnly).NeedsCompilerGeneratedCheck, Is.False);
    }
}
