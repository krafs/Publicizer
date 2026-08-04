namespace Publicizer;

/// <summary>
/// The outcome of evaluating one type or member against the publicization rules.
/// </summary>
internal enum PublicizeDecision
{
    /// <summary>No rule applies; leave accessibility untouched.</summary>
    Skip,

    /// <summary>A DoNotPublicize target names this exactly. Distinct from <see cref="Skip"/>
    /// because the walker suppresses the accessors of a denied property and logs the denial.</summary>
    DeniedExplicitly,

    /// <summary>A Publicize target names this exactly. Bypasses the compiler-generated,
    /// regex and virtual filters — see the escape hatch in docs/publicization-semantics.md.</summary>
    Explicit,

    /// <summary>Matched only by a sweep — the assembly-wide one, or a namespace or type scope
    /// narrowing it — so the filters have already been applied and <c>IncludeVirtualMembers</c>
    /// still governs the edit.</summary>
    BySweep,
}
