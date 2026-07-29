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
    /// The namespace this rule covers, recursively. Ignored when <see cref="TypeReflectionName"/> is
    /// set, since a type name already pins the namespace. Empty only for a type in the global
    /// namespace — a namespace scope always names one, since empty metadata reads as absent.
    /// </summary>
    internal string Namespace { get; set; } = "";

    /// <summary>
    /// The reflection full name of the type this rule covers, nested types included, or
    /// <see langword="null"/> when the rule covers a whole namespace.
    /// </summary>
    internal string? TypeReflectionName { get; set; }

    /// <summary>Whether this came from a <c>DoNotPublicize</c> item.</summary>
    internal bool Deny { get; set; }

    internal bool? IncludeVirtualMembers { get; set; }
    internal bool? IncludeCompilerGeneratedMembers { get; set; }
    internal Regex? MemberPattern { get; set; }

    /// <summary>
    /// How tightly this rule is drawn, for resolving overlapping scopes. A type always beats a
    /// namespace, and a longer namespace beats the namespace enclosing it.
    /// </summary>
    internal (int Rank, int NameLength) Specificity => TypeReflectionName is null
        ? (0, Namespace.Length)
        : (1, TypeReflectionName.Length);

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
