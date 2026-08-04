using System.Text.RegularExpressions;

namespace Publicizer;

/// <summary>
/// The sweep rules in force for one type, after resolving the assembly scope against any namespace
/// and type scopes that cover it.
/// </summary>
/// <remarks>
/// Immutable, and deliberately so: one instance is resolved per scope and then shared by every type
/// that scope covers, so a mutation would silently rewrite the rules for unrelated types.
/// </remarks>
internal sealed class SweepSettings
{
    /// <summary>Whether a sweep reaches this type at all. False both when nothing selects it and
    /// when a <c>DoNotPublicize</c> scope suppresses it.</summary>
    internal bool Publicize { get; init; }

    internal bool IncludeVirtualMembers { get; init; } = true;
    internal bool IncludeCompilerGeneratedMembers { get; init; } = true;
    internal Regex? MemberPattern { get; init; }

    /// <summary>
    /// Whether the compiler-generated attribute scan can affect any decision here. Hoisted so the
    /// walk runs the scan only when it matters, rather than once per member.
    /// </summary>
    internal bool NeedsCompilerGeneratedCheck => Publicize && !IncludeCompilerGeneratedMembers;

    /// <summary>
    /// These settings as narrowed by <paramref name="scope"/>, which overrides what it sets and
    /// inherits what it does not.
    /// </summary>
    internal SweepSettings NarrowedBy(PublicizeScope scope) => new()
    {
        Publicize = !scope.Deny,
        IncludeVirtualMembers = scope.IncludeVirtualMembers ?? IncludeVirtualMembers,
        IncludeCompilerGeneratedMembers = scope.IncludeCompilerGeneratedMembers ?? IncludeCompilerGeneratedMembers,
        // A scope cannot carry a pattern of its own, but the assembly's still applies inside it.
        MemberPattern = MemberPattern,
    };
}
