using System.Text.RegularExpressions;

namespace Publicizer;

/// <summary>
/// The sweep rules in force for one type, after resolving the assembly scope against any namespace
/// and type scopes that cover it.
/// </summary>
/// <remarks>
/// These used to live on the assembly and be read directly by the decision ladder. The structured
/// item form can draw a scope tighter than the assembly, so they are resolved per type instead —
/// which is what makes "publicize every member of this one type" expressible without a regex.
/// </remarks>
internal sealed class SweepSettings
{
    /// <summary>Whether a sweep reaches this type at all. False both when nothing selects it and
    /// when a <c>DoNotPublicize</c> scope suppresses it.</summary>
    internal bool Publicize { get; set; }

    internal bool IncludeVirtualMembers { get; set; } = true;
    internal bool IncludeCompilerGeneratedMembers { get; set; } = true;
    internal Regex? MemberPattern { get; set; }

    /// <summary>
    /// Whether the compiler-generated attribute scan can affect any decision here. Hoisted so the
    /// walk runs the scan only when it matters, rather than once per member.
    /// </summary>
    internal bool NeedsCompilerGeneratedCheck => Publicize && !IncludeCompilerGeneratedMembers;
}
