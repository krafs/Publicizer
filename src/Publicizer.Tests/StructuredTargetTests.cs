using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Covers the structured item form: <c>Namespace</c> and <c>Type</c> metadata on a bare-assembly
/// <c>Include</c>, how overlapping scopes resolve, and which malformed items fail the build.
/// </summary>
internal static class StructuredTargetTests
{
    private static TaskItem Item(string spec, params string[] metadata)
    {
        var item = new TaskItem(spec);
        for (int i = 0; i < metadata.Length; i += 2)
        {
            item.SetMetadata(metadata[i], metadata[i + 1]);
        }

        return item;
    }

    private static PublicizerAssemblyContext Parse(ITaskItem[] publicizes, ITaskItem[]? doNotPublicizes = null)
    {
        bool valid = PublicizeAssemblies.TryGetPublicizerAssemblyContexts(publicizes, doNotPublicizes ?? [], NullTaskLogger.Instance, out Dictionary<string, PublicizerAssemblyContext> contexts);
        Assert.That(valid, Is.True, "expected every item to parse");
        return contexts["Asm"];
    }

    private static string ErrorFor(ITaskItem item, bool deny = false)
    {
        var logger = new RecordingTaskLogger();
        ITaskItem[] publicizes = deny ? [] : [item];
        ITaskItem[] doNotPublicizes = deny ? [item] : [];

        bool valid = PublicizeAssemblies.TryGetPublicizerAssemblyContexts(publicizes, doNotPublicizes, logger, out _);

        Assert.That(valid, Is.False, "expected the item to be rejected");
        Assert.That(logger.Errors, Is.Not.Empty);
        return string.Join(" | ", logger.Errors);
    }

    private static TypePlan? Plan(PublicizerAssemblyContext context, string typeReflectionName, string typeNamespace) =>
        AssemblyPlan.Compile(context).ForType(typeReflectionName, typeNamespace);

    private static PublicizeDecision DecideMember(PublicizerAssemblyContext context, string typeReflectionName, string typeNamespace, string memberName)
    {
        TypePlan? plan = Plan(context, typeReflectionName, typeNamespace);
        return plan is null ? PublicizeDecision.Skip : plan.DecideMember(memberName, isCompilerGenerated: false);
    }

    [Test]
    public static void NamespaceScope_IsRecursiveOnSegmentBoundaries()
    {
        PublicizerAssemblyContext context = Parse([Item("Asm", "Namespace", "A.B")]);

        Assert.That(DecideMember(context, "A.B.Type", "A.B", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(DecideMember(context, "A.B.C.Type", "A.B.C", "Member"), Is.EqualTo(PublicizeDecision.BySweep));

        // "A.BX" merely starts with "A.B"; it is not inside it.
        Assert.That(Plan(context, "A.BX.Type", "A.BX"), Is.Null);
        Assert.That(Plan(context, "A.Type", "A"), Is.Null);
    }

    [Test]
    public static void TypeScope_SweepsMembers_UnlikeTheColonForm()
    {
        // The deliberate divergence between the two forms: naming a type structurally publicizes it
        // and everything in it, while the colon form publicizes only the type's own accessibility.
        PublicizerAssemblyContext structured = Parse([Item("Asm", "Namespace", "A", "Type", "Type")]);
        Assert.That(DecideMember(structured, "A.Type", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));

        PublicizerAssemblyContext colonForm = Parse([Item("Asm:A.Type")]);
        Assert.That(DecideMember(colonForm, "A.Type", "A", "Member"), Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void TypeScope_CoversNestedTypes()
    {
        PublicizerAssemblyContext context = Parse([Item("Asm", "Namespace", "A", "Type", "Outer")]);

        Assert.That(DecideMember(context, "A.Outer+Inner", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(Plan(context, "A.OuterOther", "A"), Is.Null);
    }

    [Test]
    public static void TypeScope_NamesNestedTypesWithDots()
    {
        PublicizerAssemblyContext context = Parse([Item("Asm", "Namespace", "A", "Type", "Outer.Inner")]);

        Assert.That(DecideMember(context, "A.Outer+Inner", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(Plan(context, "A.Outer", "A"), Is.Null);
    }

    [Test]
    public static void TypeScope_WithoutNamespace_IsTheGlobalNamespace()
    {
        PublicizerAssemblyContext context = Parse([Item("Asm", "Type", "GlobalType")]);

        Assert.That(DecideMember(context, "GlobalType", "", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(Plan(context, "A.GlobalType", "A"), Is.Null);
    }

    [Test]
    public static void TypeScope_BeatsAnEnclosingNamespaceScope()
    {
        PublicizerAssemblyContext context = Parse(
            [Item("Asm", "Namespace", "A", "Type", "Kept")],
            [Item("Asm", "Namespace", "A")]);

        Assert.That(DecideMember(context, "A.Kept", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(Plan(context, "A.Other", "A"), Is.Null);
    }

    [Test]
    public static void InnermostNamespaceScope_Wins()
    {
        PublicizerAssemblyContext context = Parse(
            [Item("Asm", "Namespace", "A")],
            [Item("Asm", "Namespace", "A.B")]);

        Assert.That(DecideMember(context, "A.Type", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(Plan(context, "A.B.Type", "A.B"), Is.Null);
    }

    [Test]
    public static void DoNotPublicizeScope_BeatsPublicizeScope_AtEqualSpecificity()
    {
        PublicizerAssemblyContext context = Parse(
            [Item("Asm", "Namespace", "A")],
            [Item("Asm", "Namespace", "A")]);

        Assert.That(Plan(context, "A.Type", "A"), Is.Null);
    }

    [Test]
    public static void ScopeFilters_OverrideTheAssemblySweep_AndInheritWhenAbsent()
    {
        // The assembly sweep excludes virtual members; the type scope opts back into them, and
        // inherits the compiler-generated filter it does not mention.
        PublicizerAssemblyContext context = Parse([
            Item("Asm", "IncludeVirtualMembers", "false", "IncludeCompilerGeneratedMembers", "false"),
            Item("Asm", "Namespace", "A", "Type", "Type", "IncludeVirtualMembers", "true")]);

        TypePlan scoped = Plan(context, "A.Type", "A")!;
        Assert.That(scoped.IncludeVirtualMembers, Is.True);
        Assert.That(scoped.NeedsCompilerGeneratedCheck, Is.True);

        TypePlan elsewhere = Plan(context, "B.Type", "B")!;
        Assert.That(elsewhere.IncludeVirtualMembers, Is.False);
    }

    [Test]
    public static void ScopeMemberPattern_AppliesOnlyInsideTheScope()
    {
        PublicizerAssemblyContext context = Parse([
            Item("Asm"),
            Item("Asm", "Namespace", "A", "MemberPattern", "Visible")]);

        Assert.That(DecideMember(context, "A.Type", "A", "VisibleMember"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(DecideMember(context, "A.Type", "A", "HiddenMember"), Is.EqualTo(PublicizeDecision.Skip));
        Assert.That(DecideMember(context, "B.Type", "B", "HiddenMember"), Is.EqualTo(PublicizeDecision.BySweep));
    }

    [Test]
    public static void GenericBraces_LowerToArity()
    {
        PublicizerAssemblyContext context = Parse([
            Item("Asm", "Namespace", "A", "Type", "Holder{T}"),
            Item("Asm", "Namespace", "A", "Type", "Pair{TKey,TValue}"),
            Item("Asm", "Namespace", "A", "Type", "Outer{T}.Inner{U,V}")]);

        Assert.That(DecideMember(context, "A.Holder`1", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(DecideMember(context, "A.Pair`2", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));
        Assert.That(DecideMember(context, "A.Outer`1+Inner`2", "A", "Member"), Is.EqualTo(PublicizeDecision.BySweep));

        // Only the count is read; the argument names mean nothing until Parameters lands.
        Assert.That(Plan(context, "A.Holder", "A"), Is.Null);
    }

    [Test]
    public static void ColonFormCombinedWithStructuredMetadata_IsRejected() =>
        Assert.That(ErrorFor(Item("Asm:A.Type", "Type", "Type")), Does.Contain("cannot be combined"));

    [Test]
    public static void BacktickInType_IsRejectedInFavorOfBraces() =>
        Assert.That(ErrorFor(Item("Asm", "Type", "Holder`1")), Does.Contain("MyType{T1,T2}"));

    [Test]
    public static void PlusInType_IsRejectedInFavorOfDots() =>
        Assert.That(ErrorFor(Item("Asm", "Type", "Outer+Inner")), Does.Contain("Outer.Inner"));

    [Test]
    public static void MalformedTypeArgumentList_IsRejected()
    {
        Assert.That(ErrorFor(Item("Asm", "Type", "Holder{T")), Does.Contain("unbalanced braces"));
        Assert.That(ErrorFor(Item("Asm", "Type", "Holder{}")), Does.Contain("empty type argument list"));
        Assert.That(ErrorFor(Item("Asm", "Type", "A..B")), Does.Contain("empty name segment"));

        // The commas inside would be counted as arity, so 'Holder{Dictionary{K,V}}' would silently
        // lower to Holder`2 and match nothing.
        Assert.That(ErrorFor(Item("Asm", "Type", "Holder{Dictionary{K,V}}")), Does.Contain("nested type argument list"));
    }

    [Test]
    public static void MalformedNamespace_IsRejected()
    {
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A+B")), Does.Contain("plain dotted namespace name"));
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A..B")), Does.Contain("empty name segment"));
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A.")), Does.Contain("empty name segment"));
    }

    [Test]
    public static void MemberQualifiers_AreRejectedUntilTheyAreImplemented()
    {
        foreach (string qualifier in new[] { "Field", "Method", "Property", "Event", "Accessor", "Parameters" })
        {
            Assert.That(ErrorFor(Item("Asm", "Type", "Type", qualifier, "Whatever")), Does.Contain($"'{qualifier}' metadata is not supported yet"));
        }
    }

    [Test]
    public static void DescentQualifiers_AreRejectedUntilTheyAreImplemented()
    {
        // Ignoring these would publicize more than the author asked for, silently.
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A", "IncludeSubNamespaces", "false")), Does.Contain("'IncludeSubNamespaces' metadata is not supported yet"));
        Assert.That(ErrorFor(Item("Asm", "Type", "Type", "IncludeTypeContents", "false")), Does.Contain("'IncludeTypeContents' metadata is not supported yet"));

        // Rejected on every form, not just a scope: neither has a reading anywhere else either.
        Assert.That(ErrorFor(Item("Asm", "IncludeTypeContents", "false")), Does.Contain("not supported yet"));
        Assert.That(ErrorFor(Item("Asm:A.Type", "IncludeTypeContents", "false")), Does.Contain("not supported yet"));
    }

    [Test]
    public static void ColonFormDoNotPublicizeType_BeatsAnyStructuredScope()
    {
        // Rung 4 sits above every scope: the colon form's behavior is frozen, so it wins however
        // specific the scope covering the same type is.
        PublicizerAssemblyContext context = Parse(
            [Item("Asm", "Namespace", "A", "Type", "Type")],
            [Item("Asm:A.Type")]);

        Assert.That(DecideMember(context, "A.Type", "A", "Member"), Is.EqualTo(PublicizeDecision.Skip));
    }

    [Test]
    public static void AssemblyDoNotPublicize_VetoesEveryScope()
    {
        // The assembly-wide deny is frozen behavior and stays a veto, so adding the structured form
        // cannot turn an assembly that publicized nothing into one that publicizes a namespace.
        PublicizerAssemblyContext context = Parse(
            [Item("Asm", "Namespace", "A")],
            [Item("Asm")]);

        Assert.That(Plan(context, "A.Type", "A"), Is.Null);
    }

    [Test]
    public static void ScopeFiltersOnDoNotPublicize_AreRejected()
    {
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A", "IncludeVirtualMembers", "false"), deny: true), Does.Contain("has no meaning on a DoNotPublicize scope"));
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A", "IncludeCompilerGeneratedMembers", "false"), deny: true), Does.Contain("has no meaning on a DoNotPublicize scope"));

        // Coherent as a rule, but per-member rather than per-type, which the resolver cannot express yet.
        Assert.That(ErrorFor(Item("Asm", "Namespace", "A", "MemberPattern", "Secret"), deny: true), Does.Contain("is not supported yet"));
    }

    [Test]
    public static void RejectedItems_AreAllReported_NotJustTheFirst()
    {
        var logger = new RecordingTaskLogger();

        bool valid = PublicizeAssemblies.TryGetPublicizerAssemblyContexts(
            [Item("Asm", "Type", "Holder`1"), Item("Asm", "Namespace", "A+B")],
            [],
            logger,
            out _);

        Assert.That(valid, Is.False);
        Assert.That(logger.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public static void DoNotPublicizeErrors_NameTheItemKind() =>
        Assert.That(ErrorFor(Item("Asm", "Type", "Holder`1"), deny: true), Does.StartWith("DoNotPublicize item"));
}
