using System.Text.RegularExpressions;

namespace Publicizer;

/// <summary>
/// A <see cref="PublicizerAssemblyContext"/> compiled into the structures the member walk needs.
/// </summary>
/// <remarks>
/// <para>
/// The context stores targets as flat dotted strings that are compared against
/// <c>{TypeReflectionFullName}.{MemberName}</c>. Rebuilding that concatenation for every member of
/// every type is the dominant cost of matching, so instead each target is decomposed once, up front,
/// into the (type name, member name) pairs it could denote. The member walk then does a single
/// dictionary lookup per type and plain lookups per member, never concatenating.
/// </para>
/// <para>
/// A target is ambiguous by construction: <c>A.B.C</c> may name a type, or member <c>C</c> of type
/// <c>A.B</c>, and the syntax gives no way to tell. Rather than guess, a target is indexed under
/// <em>every</em> split point, plus as a type name in its own right. That is exactly equivalent to
/// the string comparison it replaces — including the doubled dot of <c>Fixture.Shapes..ctor</c>,
/// which a naive split at the last dot would get wrong. Targets are few and user-authored, so the
/// extra entries cost nothing.
/// </para>
/// </remarks>
internal sealed class AssemblyPlan
{
    private readonly Dictionary<string, HashSet<string>> allowedMembersByType;
    private readonly Dictionary<string, HashSet<string>> deniedMembersByType;
    private readonly HashSet<string> allowedTypeNames;
    private readonly HashSet<string> deniedTypeNames;

    private AssemblyPlan(
        Dictionary<string, HashSet<string>> allowedMembersByType,
        Dictionary<string, HashSet<string>> deniedMembersByType,
        HashSet<string> allowedTypeNames,
        HashSet<string> deniedTypeNames,
        PublicizerAssemblyContext context)
    {
        this.allowedMembersByType = allowedMembersByType;
        this.deniedMembersByType = deniedMembersByType;
        this.allowedTypeNames = allowedTypeNames;
        this.deniedTypeNames = deniedTypeNames;

        PublicizeAll = context.ExplicitlyPublicizeAssembly;
        DenyAll = context.ExplicitlyDoNotPublicizeAssembly;
        IncludeVirtualMembers = context.IncludeVirtualMembers;
        MemberRegex = context.PublicizeMemberRegexPattern;
        NeedsCompilerGeneratedCheck = context.ExplicitlyPublicizeAssembly && !context.IncludeCompilerGeneratedMembers;
    }

    internal bool PublicizeAll { get; }
    internal bool DenyAll { get; }
    internal bool IncludeVirtualMembers { get; }
    internal Regex? MemberRegex { get; }

    /// <summary>
    /// Whether the compiler-generated attribute scan can affect any decision. Hoisted here so the
    /// walk runs the scan only when it matters, rather than once per member.
    /// </summary>
    internal bool NeedsCompilerGeneratedCheck { get; }

    internal static AssemblyPlan Compile(PublicizerAssemblyContext context)
    {
        var allowedMembersByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var deniedMembersByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (string target in context.PublicizeMemberPatterns)
        {
            IndexMemberSplits(target, allowedMembersByType);
        }

        foreach (string target in context.DoNotPublicizeMemberPatterns)
        {
            IndexMemberSplits(target, deniedMembersByType);
        }

        var allowedTypeNames = new HashSet<string>(context.PublicizeMemberPatterns, StringComparer.Ordinal);
        var deniedTypeNames = new HashSet<string>(context.DoNotPublicizeMemberPatterns, StringComparer.Ordinal);

        return new AssemblyPlan(allowedMembersByType, deniedMembersByType, allowedTypeNames, deniedTypeNames, context);
    }

    private static void IndexMemberSplits(string target, Dictionary<string, HashSet<string>> index)
    {
        for (int i = target.IndexOf('.'); i >= 0; i = target.IndexOf('.', i + 1))
        {
            string typeName = target.Substring(0, i);
            string memberName = target.Substring(i + 1);

            if (!index.TryGetValue(typeName, out HashSet<string>? memberNames))
            {
                memberNames = new HashSet<string>(StringComparer.Ordinal);
                index.Add(typeName, memberNames);
            }

            memberNames.Add(memberName);
        }
    }

    /// <summary>
    /// Whether a <c>DoNotPublicize</c> target names this type. Only consulted for the enclosing
    /// types the walk-up would otherwise publicize; deciding a type the walk reached on its own
    /// goes through <see cref="ForType"/>.
    /// </summary>
    internal bool IsDeniedType(string typeReflectionFullName) => deniedTypeNames.Contains(typeReflectionFullName);

    /// <summary>
    /// Returns the rules that can apply inside <paramref name="typeReflectionFullName"/>, or
    /// <see langword="null"/> when nothing in the type is reachable by any rule and the whole type
    /// can be skipped without inspecting a single member.
    /// </summary>
    internal TypePlan? ForType(string typeReflectionFullName)
    {
        _ = allowedMembersByType.TryGetValue(typeReflectionFullName, out HashSet<string>? allowedMembers);
        _ = deniedMembersByType.TryGetValue(typeReflectionFullName, out HashSet<string>? deniedMembers);
        bool allowedAsType = allowedTypeNames.Contains(typeReflectionFullName);
        bool deniedAsType = deniedTypeNames.Contains(typeReflectionFullName);

        bool hasNamedTarget = allowedMembers is not null || deniedMembers is not null || allowedAsType || deniedAsType;
        if (!hasNamedTarget && !PublicizeAll)
        {
            return null;
        }

        return new TypePlan(this, typeReflectionFullName, allowedMembers, deniedMembers, allowedAsType, deniedAsType);
    }
}
