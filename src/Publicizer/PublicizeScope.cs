using System.Text.RegularExpressions;

namespace Publicizer;

/// <summary>
/// One structured <c>Publicize</c> / <c>DoNotPublicize</c> item that names a scope rather than a
/// single member: a namespace, or a type and everything in it.
/// </summary>
/// <remarks>
/// A scope sweeps the members it encloses, subject to the filters below — unlike the colon-string
/// form, where naming a type publicizes only that type's own accessibility. The two readings coexist
/// deliberately; see docs/publicization-semantics.md.
/// </remarks>
internal sealed class PublicizeScope
{
    /// <summary>
    /// The namespace this rule covers, recursively, or empty when the rule covers a type. Exactly
    /// one of this and <see cref="TypeReflectionName"/> is ever set: a type name already carries its
    /// namespace, so keeping a second copy here would be state nothing reads and nothing maintains.
    /// A namespace scope always names a non-empty namespace, since empty metadata reads as absent.
    /// </summary>
    internal string Namespace { get; init; } = "";

    /// <summary>
    /// The reflection full name of the type this rule covers — namespace included, nested types
    /// included — or <see langword="null"/> when the rule covers a whole namespace.
    /// </summary>
    internal string? TypeReflectionName { get; init; }

    /// <summary>Whether this came from a <c>DoNotPublicize</c> item.</summary>
    internal bool Deny { get; init; }

    internal bool? IncludeVirtualMembers { get; init; }
    internal bool? IncludeCompilerGeneratedMembers { get; init; }
    internal Regex? MemberPattern { get; init; }

    /// <summary>
    /// How tightly this rule is drawn, for resolving overlapping scopes. A type always beats a
    /// namespace, and a longer name beats a shorter one.
    /// </summary>
    /// <remarks>
    /// Comparing by length is enough because length is only ever compared between two scopes that
    /// both cover the same type, and any two such scopes are prefix-related — so equal length means
    /// the same name, not a tie between unrelated scopes.
    /// </remarks>
    internal (int Rank, int NameLength) Specificity => TypeReflectionName is null
        ? (0, Namespace.Length)
        : (1, TypeReflectionName.Length);

    /// <param name="typeReflectionFullName">dnlib's <c>ReflectionFullName</c> for the type.</param>
    /// <param name="typeNamespace">The namespace the type belongs to. Both comparisons are ordinal,
    /// so a scope matches case-sensitively, as assembly names already do.</param>
    internal bool Covers(string typeReflectionFullName, string typeNamespace) => TypeReflectionName is not null
        // A type scope reaches its nested types, which extend the name with '+'.
        ? IsSelfOrUnder(typeReflectionFullName, TypeReflectionName, '+')
        // A namespace scope is recursive, but on segment boundaries: "A.B" covers "A.B.C", not "A.BX".
        : IsSelfOrUnder(typeNamespace, Namespace, '.');

    /// <summary>
    /// Whether <paramref name="candidate"/> is <paramref name="prefix"/> itself, or a name nested
    /// under it — where <paramref name="separator"/> is what joins a parent name to a child.
    /// </summary>
    private static bool IsSelfOrUnder(string candidate, string prefix, char separator)
    {
        if (candidate.Length == prefix.Length)
        {
            return candidate == prefix;
        }

        return candidate.Length > prefix.Length
            && candidate[prefix.Length] == separator
            && candidate.StartsWith(prefix, StringComparison.Ordinal);
    }
}
