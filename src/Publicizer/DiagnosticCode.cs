namespace Publicizer;

/// <summary>
/// Codes carried by every error and warning Publicizer raises, so they can be suppressed, filtered and
/// searched. One code per failure class, not per message: the malformed-<c>Type</c> spellings, for
/// instance, share a code and differ only in message text.
/// </summary>
/// <remarks>
/// <para>
/// Numbers are banded by what raises them, not by theme — a theme moves as features do, an emitter
/// does not. Within a band a new code takes the next free number.
/// </para>
/// <list type="bullet">
/// <item><c>PUB1xxx</c> — parsing and validating <c>Publicize</c>/<c>DoNotPublicize</c> items.</item>
/// <item><c>PUB2xxx</c> — task execution and I/O.</item>
/// <item><c>PUB3xxx</c> — the outcome of publicizing an assembly.</item>
/// <item><c>PUB4xxx</c> — raised from the MSBuild targets rather than the task.</item>
/// <item><c>PUB9xxx</c> — reserved for a future analyzer or BuildCheck surface.</item>
/// </list>
/// <para>
/// A code is permanent once it has appeared in a release: a retired diagnostic keeps its number
/// rather than freeing it for reuse, because consumers key <c>NoWarn</c> on it. Before a code's first
/// release it is still soft, and may be renumbered or dropped outright. Keep docs/diagnostics.md in
/// sync either way.
/// </para>
/// </remarks>
internal static class DiagnosticCode
{
    /// <summary>An item mixes the colon form with structured metadata.</summary>
    internal const string FormsMixed = "PUB1001";

    /// <summary>An item sets a member-level qualifier the structured syntax reserves but does not implement yet.</summary>
    internal const string UnsupportedMemberMetadata = "PUB1002";

    /// <summary>An item sets a qualifier that would narrow a scope's descent, which is unconditional today.</summary>
    internal const string UnsupportedDescentMetadata = "PUB1003";

    /// <summary>A scope's <c>Namespace</c> is not a plain dotted namespace name.</summary>
    internal const string InvalidNamespace = "PUB1004";

    /// <summary>A scope's <c>Type</c> is malformed.</summary>
    internal const string InvalidType = "PUB1005";

    /// <summary>A scope sets <c>MemberPattern</c>, which only the bare assembly item accepts.</summary>
    internal const string MemberPatternOnScope = "PUB1006";

    /// <summary>A <c>DoNotPublicize</c> scope sets a sweep filter, which it has no sweep to apply to.</summary>
    internal const string FilterOnDenyScope = "PUB1007";

    /// <summary><c>OutputDirectory</c> is not a usable directory path.</summary>
    internal const string InvalidOutputDirectory = "PUB2001";

    /// <summary>The log file named by <c>PublicizerLogFilePath</c> could not be created.</summary>
    internal const string LogFileNotCreated = "PUB2002";

    /// <summary>An assembly is denied as a whole while <c>Publicize</c> scopes name part of it.</summary>
    internal const string AssemblyDenyOverriddenByScopes = "PUB3001";

    /// <summary>An assembly was marked for publicization, but nothing in it was publicized.</summary>
    internal const string NothingPublicized = "PUB3002";

    /// <summary>
    /// No runtime access strategy is enabled, so the publicized members compile but fail their
    /// visibility check at run time. Raised from Krafs.Publicizer.targets, which repeats the literal.
    /// </summary>
    internal const string NoRuntimeStrategy = "PUB4001";
}
