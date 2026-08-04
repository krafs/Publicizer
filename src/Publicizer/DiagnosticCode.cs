namespace Publicizer;

/// <summary>
/// Codes carried by every error and warning the task raises, so they can be suppressed, filtered and
/// searched. One code per failure class, not per message: the malformed-<c>Type</c> spellings, for
/// instance, share a code and differ only in message text.
/// </summary>
/// <remarks>
/// Codes are permanent. A retired diagnostic keeps its number rather than freeing it for reuse, and a
/// new one takes the next free number regardless of where it fits thematically. Keep
/// docs/diagnostics.md in sync.
/// </remarks>
internal static class DiagnosticCode
{
    /// <summary>An item mixes the colon form with structured metadata.</summary>
    internal const string FormsMixed = "PUB0001";

    /// <summary>An item sets a member-level qualifier the structured syntax reserves but does not implement yet.</summary>
    internal const string UnsupportedMemberMetadata = "PUB0002";

    /// <summary>An item sets a qualifier that would narrow a scope's descent, which is unconditional today.</summary>
    internal const string UnsupportedDescentMetadata = "PUB0003";

    /// <summary>A scope's <c>Namespace</c> is not a plain dotted namespace name.</summary>
    internal const string InvalidNamespace = "PUB0004";

    /// <summary>A scope's <c>Type</c> is malformed.</summary>
    internal const string InvalidType = "PUB0005";

    /// <summary>A scope sets <c>MemberPattern</c>, which only the bare assembly item accepts.</summary>
    internal const string MemberPatternOnScope = "PUB0006";

    /// <summary>A <c>DoNotPublicize</c> scope sets a sweep filter, which it has no sweep to apply to.</summary>
    internal const string FilterOnDenyScope = "PUB0007";

    /// <summary>A scope inside another scope leaves a filter the enclosing scope sets unset, and inheritance is undecided.</summary>
    internal const string UndecidedScopeInheritance = "PUB0008";

    /// <summary>An assembly is denied as a whole while <c>Publicize</c> scopes name part of it.</summary>
    internal const string AssemblyDenyOverriddenByScopes = "PUB0009";

    /// <summary>An assembly was marked for publicization, but nothing in it was publicized.</summary>
    internal const string NothingPublicized = "PUB0010";

    /// <summary><c>OutputDirectory</c> is not a usable directory path.</summary>
    internal const string InvalidOutputDirectory = "PUB0011";

    /// <summary>The log file named by <c>PublicizerLogFilePath</c> could not be created.</summary>
    internal const string LogFileNotCreated = "PUB0012";
}
